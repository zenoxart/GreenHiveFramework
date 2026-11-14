using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Contracts;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.ModuleRegistryService;

/// <summary>
/// Publish a module version: validate -> authorize -> load previous -> decide (newer version) -> persist ->
/// register permissions -> impact analysis (workflows) -> rollout -> announce.
/// </summary>
public sealed class PublishModule(ModuleRegistry registry, Db db, ServiceClient services, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<PublishModuleResponse>> ExecuteAsync(PublishModuleRequest request, CancellationToken ct) =>
        Steps.Validate(request, r => r.Manifest is null
                ? Validation.Invalid<ModuleManifest>(Error.Field("manifest", "modules.manifest.missing", "validation:required"))
                : r.Manifest.Check())
            .Authorize(context.User, "modules.publish")
            .BindAsync(manifest => registry.VersionsAsync(manifest.ModuleId, ct).Map(versions => (Manifest: manifest, Previous: versions.FirstOrDefault())))
            .Bind(x => Decide(x.Manifest, x.Previous))
            .BindAsync(async x =>
            {
                var saved = await registry.InsertAsync(x.Manifest, request.Channel, context.User.UserName, ct);
                return saved.Map(_ => x);
            })
            .BindAsync(async x =>
            {
                // Permissions of the module become assignable to roles.
                var permissions = x.Manifest.Permissions.Select(p => new PermissionInfo(p, x.Manifest.ModuleId, p)).ToList();
                var registered = await services.PostAsync<JsonElement?>("identity-service", "/internal/permissions",
                    new RegisterPermissionsRequest(x.Manifest.ModuleId, permissions), null, ct);
                if (registered.IsFailure)
                {
                    return Result.Failure<PublishModuleResponse>(registered.Error);
                }

                var response = new PublishModuleResponse(x.Manifest.ModuleId, x.Manifest.Version) { PreviousVersion = x.Previous?.Manifest.Version };
                if (x.Previous is null)
                {
                    return response;
                }

                // Which workflows are affected? (contract surface diff -> WorkflowService)
                var impact = await services.PostAsync<ImpactAnalysisResponse>("workflow-service", "/internal/impact",
                    new ImpactAnalysisRequest(x.Manifest.ModuleId, x.Previous.Manifest.Version, x.Manifest.Version,
                        x.Previous.Manifest.ContractSurface, x.Manifest.ContractSurface), null, ct);
                var report = impact.GetValueOrDefault(null!);

                // Staged rollout (pilot users first; held when workflows would break).
                var rollout = await services.PostAsync<RolloutInfo>("update-orchestrator", "/internal/rollouts",
                    new CreateRolloutRequest(x.Manifest.ModuleId, x.Previous.Manifest.Version, x.Manifest.Version)
                    {
                        ImpactReportId = report?.ReportId,
                        BrokenWorkflows = report?.BrokenWorkflows ?? 0,
                        ReviewWorkflows = report?.ReviewWorkflows ?? 0,
                    }, null, ct);

                return response with
                {
                    ImpactReportId = report?.ReportId,
                    BrokenWorkflows = report?.BrokenWorkflows ?? 0,
                    RolloutId = rollout.TryGetValue(out var r) ? r.Id : null,
                };
            })
            .TapAsync(response => db.InTransactionAsync(session =>
                outbox.AddAsync(session, outbox.Create(EventTypes.ModulePublished, new JsonObject
                {
                    ["moduleId"] = response.ModuleId,
                    ["version"] = response.Version,
                    ["previousVersion"] = response.PreviousVersion,
                    ["brokenWorkflows"] = response.BrokenWorkflows,
                }, NotificationAudience.WithPermission("modules.publish")) with
                {
                    Title = "shell:notification.modulePublished",
                    Message = $"{response.ModuleId} {response.Version}",
                    Severity = response.BrokenWorkflows > 0 ? NotificationSeverity.Warning : NotificationSeverity.Success,
                }, ct)
                .BindAsync(_ => audit.WriteAsync(session, context.User, "module.published", "module", $"{response.ModuleId}@{response.Version}", ct: ct)), ct: ct));

    /// <summary>Pure rule: a new version must be greater than every published one.</summary>
    private static Result<(ModuleManifest Manifest, RegisteredModule? Previous)> Decide(ModuleManifest manifest, RegisteredModule? previous) =>
        previous is not null && manifest.SemanticVersion <= previous.Manifest.SemanticVersion
            ? Error.Conflict("modules.publish.versionNotNewer", $"Version {manifest.Version} is not newer than {previous.Manifest.Version}.")
            : (manifest, previous);
}
