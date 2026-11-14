using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Contracts;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.NET.Backend.UpdateOrchestrator;

/// <summary>Starts a rollout for a newly published module version (called by the Module Registry).</summary>
public sealed class StartRollout(Db db, RolloutRepository rollouts, ServiceClient services, OutboxWriter outbox, TimeProvider time)
{
    public Task<Result<RolloutInfo>> ExecuteAsync(CreateRolloutRequest request, CancellationToken ct) =>
        PilotUsersAsync(ct).BindAsync(pilots =>
        {
            var (stage, reason) = RolloutPlanner.Start(request.BrokenWorkflows);
            var now = time.GetUtcNow();
            var rollout = new RolloutInfo(Guid.NewGuid(), request.ModuleId, request.FromVersion, request.ToVersion, stage)
            {
                HoldReason = reason,
                ImpactReportId = request.ImpactReportId,
                BrokenWorkflows = request.BrokenWorkflows,
                PilotUsers = pilots,
                CreatedAt = now,
                UpdatedAt = now,
            };
            return db.InTransactionAsync(session => rollouts.InsertAsync(session, rollout, ct)
                .BindAsync(_ => Announce.StageAsync(session, outbox, rollout, ct))
                .Map(_ => rollout), ct: ct);
        });

    private async Task<Result<IReadOnlyList<Guid>>> PilotUsersAsync(CancellationToken ct)
    {
        var setting = await services.GetAsync<ConfigSetting>("configuration-service",
            $"/internal/settings/{SettingKeys.PilotUsers}?tenant={TenantId.Demo}", null, ct);
        return setting.Match(
            s => Result.Success<IReadOnlyList<Guid>>(s.Value.ValueKind == JsonValueKind.Array
                ? s.Value.EnumerateArray().Select(e => Guid.TryParse(e.GetString(), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToList()
                : []),
            error => error.Kind == ErrorKind.NotFound ? Result.Success<IReadOnlyList<Guid>>([]) : Result.Failure<IReadOnlyList<Guid>>(error));
    }
}

/// <summary>Moves a rollout to another stage (advance / hold / resume / cancel).</summary>
public sealed class ChangeRolloutStage(Db db, RolloutRepository rollouts, OutboxWriter outbox, AuditLog audit, RequestContext context)
{
    public Task<Result<RolloutInfo>> ExecuteAsync(Guid id, Func<RolloutInfo, Result<RolloutStage>> decide, string action, string? reason, CancellationToken ct) =>
        Steps.Authorize(context.User, "updates.manage")
            .BindAsync(_ => rollouts.FindAsync(id, ct))
            .Bind(rollout => decide(rollout).Map(stage => rollout with
            {
                Stage = stage,
                HoldReason = stage == RolloutStage.Held ? reason ?? "Held by an administrator." : null,
                UpdatedAt = context.Now,
            }))
            .BindAsync(updated => db.InTransactionAsync(session =>
                rollouts.SetStageAsync(session, updated.Id, updated.Stage, updated.HoldReason, updated.UpdatedAt, ct)
                    .BindAsync(_ => Announce.StageAsync(session, outbox, updated, ct))
                    .BindAsync(_ => audit.WriteAsync(session, context.User, $"rollout.{action}", "rollout", updated.Id.ToString(),
                        details: new JsonObject { ["stage"] = updated.Stage.ToString(), ["module"] = $"{updated.ModuleId}@{updated.ToVersion}" }, ct: ct))
                    .Map(_ => updated), ct: ct));
}

internal static class Announce
{
    /// <summary>Tells the affected clients that a version is available (pilot users or everyone) and admins about the stage.</summary>
    public static async Task<Result<Unit>> StageAsync(DbSession session, OutboxWriter outbox, RolloutInfo rollout, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["rolloutId"] = rollout.Id.ToString(),
            ["moduleId"] = rollout.ModuleId,
            ["version"] = rollout.ToVersion,
            ["stage"] = rollout.Stage.ToString(),
        };

        var audiences = rollout.Stage switch
        {
            RolloutStage.Pilot => rollout.PilotUsers.Select(NotificationAudience.ForUser).ToList(),
            RolloutStage.Broad => [NotificationAudience.Everyone],
            _ => [],
        };
        foreach (var audience in audiences)
        {
            var added = await outbox.AddAsync(session, outbox.Create(EventTypes.ModuleVersionAvailable, payload.DeepClone().AsObject(), audience) with
            {
                Title = "shell:notification.moduleUpdate",
                Message = $"{rollout.ModuleId} {rollout.ToVersion}",
                Severity = NotificationSeverity.Info,
            }, ct);
            if (added.IsFailure)
            {
                return added;
            }
        }

        return await outbox.AddAsync(session, outbox.Create(EventTypes.ModuleUpdateStatus, payload, NotificationAudience.WithPermission("updates.manage")) with
        {
            Title = "shell:notification.rolloutStage",
            Message = $"{rollout.ModuleId} {rollout.ToVersion}: {rollout.Stage}",
            Severity = rollout.Stage == RolloutStage.Held ? NotificationSeverity.Warning : NotificationSeverity.Info,
        }, ct);
    }
}
