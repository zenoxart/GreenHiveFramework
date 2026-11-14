using System.Net.Http.Headers;
using GreenHive.NET.Contracts;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Security;

/// <summary>Connection settings (roaming): server URL and the last user name.</summary>
public sealed class ConnectionSettings
{
    public string ServerUrl { get; set; } = "http://localhost:8080";

    public string? LastUserName { get; set; }

    public bool RememberMe { get; set; } = true;
}

public enum ConnectionState
{
    SignedOut,
    Online,
    Offline,
}

/// <summary>
/// The client's connection to the API gateway: sign-in, session restore, token refresh, permissions and Result-based
/// HTTP calls. Every call returns a Result; an unreachable server switches the state to Offline (and back on success).
/// </summary>
public sealed class BackendConnection : AppObject
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private ConnectionSettings? _settings;
    private JsonController<ConnectionSettings>? _json;
    private ConnectionState _state = ConnectionState.SignedOut;

    public event EventHandler? SignedIn;

    public event EventHandler? SignedOut;

    public event EventHandler? StateChanged;

    /// <summary>Raised when the permissions of the signed-in user changed (token refreshed).</summary>
    public event EventHandler? PermissionsChanged;

    public ConnectionSettings Settings => _settings ??= Json.TryLoad(SettingsPath) ?? new ConnectionSettings();

    public string ServerUrl => Settings.ServerUrl.TrimEnd('/');

    public UserInfo? CurrentUser { get; private set; }

    public string? AccessToken { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsSignedIn => AccessToken is not null && CurrentUser is not null;

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                AppContext.Log.Write($"Connection state: {value}", value == ConnectionState.Offline ? LogEntryType.Warning : LogEntryType.Information, nameof(BackendConnection));
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The signed-in user as seen by modules.</summary>
    public ModuleUser ModuleUser => CurrentUser is { } u
        ? new ModuleUser(u.Id, u.UserName, u.DisplayName, u.Permissions.ToList())
        : ModuleUser.Anonymous;

    private string SettingsPath => Path.Combine(AppContext.RoamingDataPath, "Connection.json");

    private JsonController<ConnectionSettings> Json => _json ??= AppContext.Produce<JsonController<ConnectionSettings>>();

    private ITokenStore Tokens => (AppContext as ITokenStoreContext)?.Tokens ?? NoTokenStoreInstance;

    private static readonly ITokenStore NoTokenStoreInstance = new NoTokenStore();

    public bool Has(string? permission) =>
        permission is null || (CurrentUser is { } user && (user.Permissions.Contains("*") || user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)));

    public async Task<Result<UserInfo>> SignInAsync(string serverUrl, string userName, string password, bool rememberMe, CancellationToken ct = default)
    {
        Settings.ServerUrl = string.IsNullOrWhiteSpace(serverUrl) ? Settings.ServerUrl : serverUrl.Trim().TrimEnd('/');
        Settings.LastUserName = userName;
        Settings.RememberMe = rememberMe;
        Json.TrySave(Settings, SettingsPath);

        var response = await HttpResults.PostAsync<LoginResponse>(_http, $"{ServerUrl}/api/identity/login", new LoginRequest(userName, password), ct);
        return response.Tap(login => Accept(login, rememberMe)).Map(login => login.User).TapError(TrackOffline);
    }

    /// <summary>Restores a remembered session (token still valid) and refreshes it.</summary>
    public async Task<Result<UserInfo>> RestoreSessionAsync(CancellationToken ct = default)
    {
        if (Tokens.Load(ServerUrl) is not { } stored || stored.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return Error.Unauthorized("auth.session.none", "auth:sessionExpired");
        }

        AccessToken = stored.AccessToken;
        ExpiresAt = stored.ExpiresAt;
        var refreshed = await SendCoreAsync<LoginResponse>(HttpMethod.Post, "/api/identity/refresh", null, ct);
        if (refreshed.TryGetValue(out var login, out var error))
        {
            Accept(login, Settings.RememberMe);
            return login.User;
        }

        AccessToken = null;
        if (error.Kind == ErrorKind.Unauthorized)
        {
            Tokens.Clear();
        }

        return error;
    }

    /// <summary>Gets a fresh token with the current permissions (e.g. after permissions.changed).</summary>
    public async Task<Result<Unit>> RefreshAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            var refreshed = await SendCoreAsync<LoginResponse>(HttpMethod.Post, "/api/identity/refresh", null, ct);
            return refreshed.Tap(login =>
            {
                var permissionsChanged = CurrentUser is null || !CurrentUser.Permissions.SequenceEqual(login.User.Permissions);
                Accept(login, Settings.RememberMe, raiseSignedIn: false);
                if (permissionsChanged)
                {
                    PermissionsChanged?.Invoke(this, EventArgs.Empty);
                }
            }).ToUnit();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void SignOut()
    {
        Tokens.Clear();
        AccessToken = null;
        CurrentUser = null;
        State = ConnectionState.SignedOut;
        AppContext.Log.Write("Signed out", LogEntryType.Information, nameof(BackendConnection));
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Calls the gateway with the bearer token; on 401 the token is refreshed once and the call repeated.</summary>
    public async Task<Result<T>> SendAsync<T>(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        if (!IsSignedIn)
        {
            return Error.Unauthorized("auth.required", "auth:required");
        }

        if (ExpiresAt < DateTimeOffset.UtcNow.AddMinutes(2))
        {
            await RefreshAsync(ct);
        }

        var result = await SendCoreAsync<T>(method, path, body, ct);
        if (result.IsFailure && result.Error.Kind == ErrorKind.Unauthorized && (await RefreshAsync(ct)).IsSuccess)
        {
            result = await SendCoreAsync<T>(method, path, body, ct);
        }

        return result;
    }

    public Task<Result<T>> GetAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Get, path, null, ct);

    public Task<Result<T>> PostAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, path, body, ct);

    public Task<Result<T>> PutAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Put, path, body, ct);

    /// <summary>A request with bearer token for streaming or downloads (event stream, packages).</summary>
    public HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{ServerUrl}/{path.TrimStart('/')}");
        if (AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        return request;
    }

    public HttpClient Http => _http;

    /// <summary>Client for a module backend route (base path, e.g. "/api/documents").</summary>
    public IModuleApi CreateApi(string routePrefix) => new ApiClient(this, routePrefix);

    /// <summary>Marks the server reachable again (e.g. after the event stream reconnected).</summary>
    public void ReportOnline()
    {
        if (IsSignedIn)
        {
            State = ConnectionState.Online;
        }
    }

    private async Task<Result<T>> SendCoreAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var request = HttpResults.WithBody(method, $"{ServerUrl}/{path.TrimStart('/')}", body);
        if (AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        var result = await HttpResults.SendAsync<T>(_http, request, ct);
        if (result.IsSuccess && IsSignedIn)
        {
            State = ConnectionState.Online;
        }

        return result.TapError(TrackOffline);
    }

    private void TrackOffline(Error error)
    {
        if (error.Kind is ErrorKind.Unavailable && IsSignedIn)
        {
            State = ConnectionState.Offline;
        }
    }

    private void Accept(LoginResponse login, bool rememberMe, bool raiseSignedIn = true)
    {
        AccessToken = login.AccessToken;
        ExpiresAt = login.ExpiresAt;
        CurrentUser = login.User;
        if (rememberMe)
        {
            Tokens.Save(new StoredSession(ServerUrl, login.User.UserName, login.AccessToken, login.ExpiresAt));
        }

        State = ConnectionState.Online;
        if (raiseSignedIn)
        {
            AppContext.Log.Write($"Signed in as {login.User.DisplayName} ({ServerUrl})", LogEntryType.Information, nameof(BackendConnection));
            SignedIn?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>IModuleApi for one module backend route; relative URLs are appended to the route.</summary>
public sealed class ApiClient(BackendConnection connection, string routePrefix) : IModuleApi
{
    private readonly string _prefix = "/" + routePrefix.Trim('/');

    public Task<Result<T>> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken = default) =>
        connection.SendAsync<T>(HttpMethod.Get, Combine(relativeUrl), null, cancellationToken);

    public Task<Result<T>> PostAsync<T>(string relativeUrl, object? body, CancellationToken cancellationToken = default) =>
        connection.SendAsync<T>(HttpMethod.Post, Combine(relativeUrl), body, cancellationToken);

    public Task<Result<T>> PutAsync<T>(string relativeUrl, object? body, CancellationToken cancellationToken = default) =>
        connection.SendAsync<T>(HttpMethod.Put, Combine(relativeUrl), body, cancellationToken);

    public Task<Result<Unit>> DeleteAsync(string relativeUrl, CancellationToken cancellationToken = default) =>
        connection.SendAsync<Unit>(HttpMethod.Delete, Combine(relativeUrl), null, cancellationToken);

    private string Combine(string relativeUrl) =>
        relativeUrl.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ? relativeUrl
        : string.IsNullOrEmpty(relativeUrl) ? _prefix
        : relativeUrl.StartsWith('?') ? _prefix + relativeUrl
        : $"{_prefix}/{relativeUrl.TrimStart('/')}";
}
