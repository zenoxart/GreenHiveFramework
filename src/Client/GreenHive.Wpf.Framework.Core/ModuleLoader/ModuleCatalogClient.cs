using GreenHive.NET.Contracts.Api;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>The Module Registry's catalogue for the signed-in user and the rollout assignments.</summary>
public sealed class ModuleCatalogClient : AppObject
{
    private BackendConnection Backend => Context<ISecurityContext>().Backend;

    public Task<Result<IReadOnlyList<ModuleCatalogEntry>>> CatalogAsync(HostInfo host, CancellationToken ct) =>
        Backend.GetAsync<IReadOnlyList<ModuleCatalogEntry>>(
            $"/api/modules/catalog?hostVersion={Uri.EscapeDataString(host.HostVersion)}&desktopRuntime={Uri.EscapeDataString(host.DesktopRuntimeVersion)}", ct);

    public Task<Result<IReadOnlyList<ModuleAssignment>>> AssignmentsAsync(CancellationToken ct) =>
        Backend.GetAsync<IReadOnlyList<ModuleAssignment>>("/api/updates/assignments", ct);

    public Task<Result<IReadOnlyList<ModuleVersionInfo>>> VersionsAsync(CancellationToken ct) =>
        Backend.GetAsync<IReadOnlyList<ModuleVersionInfo>>("/api/modules", ct);

    public Task<Result<PublishModuleResponse>> PublishAsync(ModuleManifest manifest, CancellationToken ct) =>
        Backend.PostAsync<PublishModuleResponse>("/api/modules", new PublishModuleRequest(manifest), ct);
}
