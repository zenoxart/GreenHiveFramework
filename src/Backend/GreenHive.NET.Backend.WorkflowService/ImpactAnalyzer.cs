using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Contracts;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>
/// Determines which workflows are affected by a module update: diff of the contract surfaces -> dependency index ->
/// classification (Compatible / AutoMigrated / ReviewRequired / Broken) -> automatic migrations as new versions ->
/// NeedsReview + suspended AI triggers -> running instances paused when broken -> report + event.
/// </summary>
public sealed class ImpactAnalyzer(
    WorkflowRepository repository,
    WorkflowEngine engine,
    ModuleDirectory modules,
    Db db,
    OutboxWriter outbox,
    ServiceClient services,
    TimeProvider time,
    ILogger<ImpactAnalyzer> logger)
{
    public async Task<Result<ImpactAnalysisResponse>> AnalyzeAsync(ImpactAnalysisRequest request, CancellationToken ct)
    {
        var diff = ModuleContractSurface.Diff(request.Old, request.New);
        var ids = diff.Select(c => c.Id).Append(request.ModuleId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var affected = await repository.DependingOnAsync(ids, ct);
        if (!affected.TryGetValue(out var workflows, out var error))
        {
            return error;
        }

        var autoMigrate = await AutoMigrateAsync(ct);
        var impacts = new List<WorkflowImpact>();
        var events = new List<NotificationEnvelope>();
        var toSave = new List<(WorkflowDefinition Definition, bool Insert)>();
        var toPause = new List<Guid>();

        foreach (var workflow in workflows)
        {
            var analysis = WorkflowMigration.Analyze(workflow, diff, request.New);
            var running = (await repository.RunningOfAsync(workflow.Id, ct)).GetValueOrDefault([]);
            var impact = new WorkflowImpact
            {
                WorkflowId = workflow.Id,
                Name = workflow.Name,
                Version = workflow.Version,
                Severity = analysis.Severity,
                Reasons = analysis.Reasons,
                RunningInstances = running.Count,
            };

            if (analysis.Migrated is { } migrated && autoMigrate)
            {
                var next = migrated with
                {
                    Version = workflow.Version + 1,
                    Status = analysis.Severity >= ImpactSeverity.ReviewRequired ? WorkflowStatus.NeedsReview : workflow.Status,
                    UpdatedAt = time.GetUtcNow(),
                };
                next = next with
                {
                    Dependencies = WorkflowAnalysis.ComputeDependencies(next, await modules.ModuleOfAsync(ct),
                        id => id.Equals(request.ModuleId, StringComparison.OrdinalIgnoreCase) ? request.ToVersion : null),
                };
                impact = impact with { MigratedToVersion = next.Version, SuggestedAction = "none" };
                toSave.Add((Suspend(next, analysis.Severity), true));
            }
            else if (analysis.Severity >= ImpactSeverity.ReviewRequired)
            {
                toSave.Add((Suspend(workflow with { Status = WorkflowStatus.NeedsReview }, analysis.Severity), false));
            }

            if (analysis.Severity >= ImpactSeverity.ReviewRequired)
            {
                var suspended = workflow.Triggers.Any(t => t.Executor == WorkflowExecutor.Ai && t.Enabled);
                impact = impact with
                {
                    AiTriggersSuspended = suspended,
                    SuggestedAction = analysis.Severity == ImpactSeverity.Broken ? "open in designer or pin to the old module version" : "review in designer",
                    InstanceAction = analysis.Severity == ImpactSeverity.Broken && running.Count > 0 ? "paused" : "continued",
                };
                if (analysis.Severity == ImpactSeverity.Broken)
                {
                    toPause.AddRange(running.Select(r => r.Id));
                }

                if (suspended)
                {
                    events.Add(outbox.Create(EventTypes.WorkflowTriggerSuspended, new JsonObject
                    {
                        ["workflowId"] = workflow.Id.ToString(),
                        ["workflowName"] = workflow.Name,
                        ["moduleId"] = request.ModuleId,
                    }, NotificationAudience.WithPermission("workflows.edit")) with
                    {
                        Title = "workflow:notification.triggerSuspended",
                        Message = workflow.Name,
                        Severity = NotificationSeverity.Warning,
                    });
                }
            }

            impacts.Add(impact);
        }

        var report = new WorkflowImpactReport
        {
            ModuleId = request.ModuleId,
            FromVersion = request.FromVersion,
            ToVersion = request.ToVersion,
            AnalyzedAt = time.GetUtcNow(),
            RolloutStage = "pilot",
            SurfaceDiff = diff,
            Workflows = impacts,
        };
        events.Add(outbox.Create(EventTypes.WorkflowImpactDetected, new JsonObject
        {
            ["reportId"] = report.Id.ToString(),
            ["moduleId"] = request.ModuleId,
            ["toVersion"] = request.ToVersion,
            ["broken"] = report.BrokenCount,
            ["review"] = report.ReviewCount,
        }, NotificationAudience.WithPermission("workflows.edit")) with
        {
            Title = "workflow:notification.impactDetected",
            Message = $"{request.ModuleId} {request.ToVersion}: {impacts.Count} workflow(s), {report.BrokenCount} broken",
            Severity = report.BrokenCount > 0 ? NotificationSeverity.Warning : NotificationSeverity.Info,
            NavigationTarget = "workflow.impact",
            NavigationParameter = report.Id.ToString(),
        });

        var saved = await db.InTransactionAsync(async session =>
        {
            foreach (var (definition, insert) in toSave)
            {
                var result = insert
                    ? await repository.InsertDefinitionAsync(session, definition, TenantId.Demo.Value, ct)
                    : await repository.UpdateDefinitionAsync(session, definition, ct);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            var stored = await repository.SaveReportAsync(session, report, ct);
            foreach (var envelope in events)
            {
                if (stored.IsSuccess)
                {
                    stored = await outbox.AddAsync(session, envelope, ct);
                }
            }

            return stored;
        }, ct: ct);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        foreach (var instanceId in toPause.Distinct())
        {
            await engine.PauseAsync(instanceId, $"Paused by the update of {request.ModuleId} to {request.ToVersion} (workflow broken).", ct);
        }

        modules.Invalidate();
        logger.LogInformation("Impact of {Module} {From}->{To}: {Count} workflows, {Broken} broken, {Review} review",
            request.ModuleId, request.FromVersion, request.ToVersion, impacts.Count, report.BrokenCount, report.ReviewCount);
        return new ImpactAnalysisResponse(report.Id, impacts.Count, report.BrokenCount, report.ReviewCount, impacts.Count(i => i.MigratedToVersion is not null))
        {
            SurfaceDiff = diff,
        };
    }

    private static WorkflowDefinition Suspend(WorkflowDefinition definition, ImpactSeverity severity) =>
        severity < ImpactSeverity.ReviewRequired
            ? definition
            : definition with
            {
                Triggers = definition.Triggers.Select(t => t.Executor == WorkflowExecutor.Ai && t.Enabled
                    ? t with { Suspended = true, SuspendedReason = "Suspended by the impact analysis of a module update - review the workflow." }
                    : t).ToList(),
            };

    private async Task<bool> AutoMigrateAsync(CancellationToken ct)
    {
        var setting = await services.GetAsync<ConfigSetting>("configuration-service",
            $"/internal/settings/{SettingKeys.AutoMigrateWorkflows}?tenant={TenantId.Demo}", null, ct);
        return setting.Match(s => s.Value.ValueKind != JsonValueKind.False, _ => true);
    }
}
