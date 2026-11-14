using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.UpdateOrchestrator;

public sealed class RolloutRepository(Db db)
{
    public Task<Result<IReadOnlyList<RolloutInfo>>> ListAsync(CancellationToken ct) =>
        db.QueryAsync("select * from updates.rollouts order by created_at desc limit 200", Map, null, ct);

    /// <summary>The latest rollout of every module.</summary>
    public Task<Result<IReadOnlyList<RolloutInfo>>> LatestPerModuleAsync(CancellationToken ct) =>
        db.QueryAsync("select distinct on (module_id) * from updates.rollouts order by module_id, created_at desc", Map, null, ct);

    public Task<Result<RolloutInfo>> FindAsync(Guid id, CancellationToken ct) =>
        db.FindAsync("select * from updates.rollouts where id = @id", Map, new { id }, Error.NotFound("updates.rollout.notFound", "updates:rollout.notFound"), ct);

    public Task<Result<Unit>> InsertAsync(DbSession session, RolloutInfo r, CancellationToken ct) =>
        session.ExecuteAsync(
            """
            insert into updates.rollouts (id, module_id, from_version, to_version, stage, hold_reason, impact_report_id, broken_workflows, pilot_users, created_at, updated_at)
            values (@id, @moduleId, @fromVersion, @toVersion, @stage, @holdReason, @impactReportId, @broken, @pilots, @now, @now)
            """,
            new
            {
                id = r.Id,
                moduleId = r.ModuleId,
                fromVersion = r.FromVersion,
                toVersion = r.ToVersion,
                stage = r.Stage,
                holdReason = r.HoldReason,
                impactReportId = r.ImpactReportId,
                broken = r.BrokenWorkflows,
                pilots = r.PilotUsers.ToArray(),
                now = r.CreatedAt,
            }, ct).ToUnit();

    public Task<Result<Unit>> SetStageAsync(DbSession session, Guid id, RolloutStage stage, string? holdReason, DateTimeOffset now, CancellationToken ct) =>
        session.ExecuteExpectingAsync("update updates.rollouts set stage = @stage, hold_reason = @holdReason, updated_at = @now where id = @id",
            new { id, stage, holdReason, now }, 1, Error.Conflict("updates.rollout.concurrency", "errors:concurrency"), ct);

    private static RolloutInfo Map(System.Data.Common.DbDataReader r) => new(
        r.Get<Guid>("id"), r.Get<string>("module_id"), r.GetOrNull<string>("from_version"), r.Get<string>("to_version"), r.GetEnum<RolloutStage>("stage"))
    {
        HoldReason = r.GetOrNull<string>("hold_reason"),
        ImpactReportId = r.GetNullable<Guid>("impact_report_id"),
        BrokenWorkflows = r.Get<int>("broken_workflows"),
        PilotUsers = r.GetOrNull<Guid[]>("pilot_users") ?? [],
        CreatedAt = r.GetTime("created_at"),
        UpdatedAt = r.GetTime("updated_at"),
    };
}
