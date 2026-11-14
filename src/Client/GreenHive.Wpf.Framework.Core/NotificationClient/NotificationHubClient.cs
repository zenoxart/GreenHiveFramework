using System.Net;
using System.Text;
using System.Text.Json;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.NotificationClient;

/// <summary>Persisted position in the event stream (catch-up after a restart).</summary>
public sealed class NotificationStreamState
{
    public long LastSequence { get; set; }
}

/// <summary>
/// Keeps a server-sent events connection to /api/notifications/stream while signed in. Reconnects with back-off,
/// resumes with Last-Event-ID (no event is lost while offline), refreshes the token on 401 and hands every event to the
/// router on the UI thread. "permissions.changed" refreshes the token so new permissions apply immediately.
/// </summary>
public sealed class NotificationHubClient : AppObject
{
    private readonly HttpClient _stream = new() { Timeout = Timeout.InfiniteTimeSpan };
    private CancellationTokenSource? _cts;
    private NotificationStreamState? _state;
    private JsonController<NotificationStreamState>? _json;
    private bool _connected;

    public event EventHandler? ConnectionChanged;

    public bool IsConnected
    {
        get => _connected;
        private set
        {
            if (_connected != value)
            {
                _connected = value;
                AppContext.Dispatcher?.Run(() => ConnectionChanged?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    public long LastSequence => State.LastSequence;

    private BackendConnection Backend => Context<ISecurityContext>().Backend;

    private NotificationRouter Router => Context<INotificationContext>().Router;

    private string StatePath => Path.Combine(AppContext.LocalDataPath, "Notifications.json");

    private NotificationStreamState State => _state ??= Json.TryLoad(StatePath) ?? new NotificationStreamState();

    private JsonController<NotificationStreamState> Json => _json ??= AppContext.Produce<JsonController<NotificationStreamState>>();

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        IsConnected = false;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var delays = new[] { 1, 2, 5, 10, 30 };
        var attempt = 0;
        if (State.LastSequence == 0)
        {
            // First start on this device: begin at the current end of the stream instead of replaying history.
            var status = await Backend.GetAsync<JsonElement>("/api/notifications/status", ct);
            if (status.TryGetValue(out var s) && s.TryGetProperty("latestSequence", out var latest))
            {
                State.LastSequence = Math.Max(0, latest.GetInt64() - 20);
            }
        }

        while (!ct.IsCancellationRequested && Backend.IsSignedIn)
        {
            var result = await ListenAsync(ct);
            IsConnected = false;
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (result.IsFailure && result.Error.Kind == ErrorKind.Unauthorized)
            {
                await Backend.RefreshAsync(ct);
            }

            var delay = TimeSpan.FromSeconds(delays[Math.Min(attempt++, delays.Length - 1)]);
            AppContext.Log.Write($"Event stream disconnected ({(result.IsFailure ? result.Error.Code : "closed")}), reconnecting in {delay.TotalSeconds:0}s",
                LogEntryType.Warning, nameof(NotificationHubClient));
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result.IsSuccess)
            {
                attempt = 0;
            }
        }
    }

    private async Task<Result<Unit>> ListenAsync(CancellationToken ct)
    {
        try
        {
            using var request = Backend.CreateRequest(HttpMethod.Get, $"/api/notifications/stream?after={State.LastSequence}");
            request.Headers.Accept.ParseAdd("text/event-stream");
            if (State.LastSequence > 0)
            {
                request.Headers.TryAddWithoutValidation("Last-Event-ID", State.LastSequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            using var response = await _stream.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Error.Unauthorized("notifications.unauthorized", "auth:sessionExpired");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Error.Unavailable($"notifications.http.{(int)response.StatusCode}", "errors:unavailable");
            }

            IsConnected = true;
            Backend.ReportOnline();
            AppContext.Log.Write("Event stream connected", LogEntryType.Information, nameof(NotificationHubClient));
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? id = null;
            var data = new StringBuilder();
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    if (data.Length > 0)
                    {
                        Dispatch(id, data.ToString());
                    }

                    id = null;
                    data.Clear();
                }
                else if (line.StartsWith("id:", StringComparison.Ordinal))
                {
                    id = line[3..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    data.Append(line[5..].TrimStart());
                }
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Error.Unavailable("notifications.connection", ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Result.Success();
        }
    }

    private void Dispatch(string? id, string json)
    {
        NotificationEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<NotificationEnvelope>(json, GreenHiveJson.Options);
        }
        catch (JsonException ex)
        {
            AppContext.Log.Write($"Invalid event: {ex.Message}", LogEntryType.Warning, nameof(NotificationHubClient));
            return;
        }

        if (envelope is null)
        {
            return;
        }

        if (long.TryParse(id, out var sequence) && sequence > State.LastSequence)
        {
            State.LastSequence = sequence;
            Json.TrySave(State, StatePath);
        }

        if (envelope.Type == EventTypes.PermissionsChanged)
        {
            _ = Backend.RefreshAsync();
        }

        AppContext.Dispatcher?.Run(() => Router.Publish(envelope));
    }
}
