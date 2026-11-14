using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Security;

/// <summary>
/// Permissions of the signed-in user on the client (for showing/enabling UI). The server authorizes every operation
/// again - client checks are convenience, never security.
/// </summary>
public sealed class PermissionService : AppObject
{
    public event EventHandler? Changed;

    private BackendConnection Backend => Context<ISecurityContext>().Backend;

    public bool Has(string? permission) => Backend.Has(permission);

    public IReadOnlyList<string> All => Backend.CurrentUser?.Permissions ?? [];

    protected override void OnCreated()
    {
        Backend.SignedIn += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Backend.SignedOut += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Backend.PermissionsChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }
}
