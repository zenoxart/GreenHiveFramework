using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Security;

/// <summary>
/// Profile of the signed-in user: profile picture (loaded after sign-in, cached locally for offline starts),
/// changing the picture and the password. Pictures of other users are cached in memory.
/// </summary>
public sealed class UserProfileService : AppObject
{
    private readonly Dictionary<Guid, byte[]?> _others = [];

    /// <summary>Raised when the signed-in user or the picture changed.</summary>
    public event EventHandler? Changed;

    /// <summary>PNG/JPEG bytes of the signed-in user's picture (null = no picture, show initials).</summary>
    public byte[]? Avatar { get; private set; }

    public UserInfo? User => Backend.CurrentUser;

    /// <summary>Two-letter initials of the display name ("Maria Huber" -> "MH").</summary>
    public string Initials => User is { } u ? InitialsOf(u.DisplayName) : string.Empty;

    private BackendConnection Backend => Context<ISecurityContext>().Backend;

    private string CacheFolder => Directory.CreateDirectory(Path.Combine(AppContext.LocalDataPath, "Avatars")).FullName;

    public static string InitialsOf(string displayName) =>
        string.Concat(displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    protected override void OnCreated()
    {
        Backend.SignedIn += (_, _) => _ = LoadAsync();
        Backend.SignedOut += (_, _) =>
        {
            Avatar = null;
            _others.Clear();
            Raise();
        };
        if (Backend.IsSignedIn)
        {
            _ = LoadAsync();
        }
    }

    /// <summary>Shows the cached picture at once, then fetches the current one from the server.</summary>
    public async Task LoadAsync()
    {
        if (User is not { } user)
        {
            return;
        }

        var cache = Path.Combine(CacheFolder, user.Id.ToString("N"));
        if (Avatar is null && File.Exists(cache))
        {
            Avatar = await File.ReadAllBytesAsync(cache);
            Raise();
        }

        var result = await Backend.GetAsync<AvatarInfo>("/api/identity/me/avatar");
        if (result.TryGetValue(out var avatar, out var error))
        {
            Avatar = Convert.FromBase64String(avatar.DataBase64);
            await File.WriteAllBytesAsync(cache, Avatar);
        }
        else if (error.Kind == ErrorKind.NotFound)
        {
            Avatar = null;
            File.Delete(cache);
        }

        Raise();
    }

    /// <summary>Uploads a new picture (already scaled; PNG or JPEG, max 512 KB).</summary>
    public async Task<Result<Unit>> SetAvatarAsync(byte[] image, string contentType = "image/png")
    {
        var result = await Backend.PutAsync<AvatarInfo>("/api/identity/me/avatar", new AvatarInfo(contentType, Convert.ToBase64String(image)));
        if (result.IsSuccess && User is { } user)
        {
            Avatar = image;
            await File.WriteAllBytesAsync(Path.Combine(CacheFolder, user.Id.ToString("N")), image);
            Raise();
        }

        return result.ToUnit();
    }

    public async Task<Result<Unit>> RemoveAvatarAsync()
    {
        var result = await Backend.SendAsync<Unit>(HttpMethod.Delete, "/api/identity/me/avatar");
        if (result.IsSuccess && User is { } user)
        {
            Avatar = null;
            File.Delete(Path.Combine(CacheFolder, user.Id.ToString("N")));
            Raise();
        }

        return result;
    }

    public Task<Result<Unit>> ChangePasswordAsync(string currentPassword, string newPassword) =>
        Backend.PutAsync<Unit>("/api/identity/me/password", new ChangePasswordRequest(currentPassword, newPassword));

    /// <summary>Picture of another user (null = none); cached for the session.</summary>
    public async Task<byte[]?> AvatarOfAsync(Guid userId)
    {
        if (User?.Id == userId)
        {
            return Avatar;
        }

        if (_others.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var result = await Backend.GetAsync<AvatarInfo>($"/api/identity/users/{userId}/avatar");
        var bytes = result.TryGetValue(out var avatar) ? Convert.FromBase64String(avatar.DataBase64) : null;
        _others[userId] = bytes;
        return bytes;
    }

    private void Raise()
    {
        if (AppContext.Dispatcher is { } dispatcher)
        {
            dispatcher.Run(() => Changed?.Invoke(this, EventArgs.Empty));
        }
        else
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
