namespace GreenHive.Wpf.Framework.Security;

/// <summary>Application contexts that offer the backend connection.</summary>
public interface ISecurityContext
{
    BackendConnection Backend { get; }

    /// <summary>Profile (picture, password) of the signed-in user and pictures of other users.</summary>
    UserProfileService Profile { get; }
}
