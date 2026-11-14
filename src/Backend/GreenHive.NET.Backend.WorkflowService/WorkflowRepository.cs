using System.Text.Json;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>
/// Storage of workflow definitions (full version history), the dependency index, instances (append-only history)
/// and impact reports. Adapter only - no business rules.
/// </summary>
public sealed class WorkflowRepository(Db db)
{
    private static readonly JsonSerializerOptions Json = GreenHiveJson.Options;

    // ------------------------------------------------------------ definitions

    /// <summary>The latest version of every workflow of a tenant.</summary>
    public Task<Result<IReadOnlyList<WorkflowDefinition>>> LatestAsync(Guid tenantId, CancellationToken ct) =>
        db.QueryAsync(
            """
            select distinct on (id) definition::text as definition, status from workflow.definitions
            where tenant_id = @tenantId order by id, version desc
            """, MapDefinition, new { tenantId }, ct);

    public Task<Result<WorkflowDefinition>> FindLatestAsync(Guid id, CancellationToken ct) =>
        db.FindAsync("select definition::text as definition, status from workflow.definitions where id = @id order by version desc limit 1",
            MapDefinition, new { id }, NotFound(id), ct);

    public Task<Result<WorkflowDefinition>> FindVersionAsync(Guid id, int version, CancellationToken ct) =>
        db.FindAsync("select definition::text as definition, status from workflow.definitions where id = @id and version = @version",
            MapDefinition, new { id, version }, NotFound(id), ct);

    public Task<Result<IReadOnlyList<WorkflowDefinition>>> VersionsAsync(Guid id, CancellationToken ct) =>
        db.QueryAsync("select definition::text as definition, status from workflow.definitions where id = @id order by version desc",
            MapDefinition, new { id }, ct);

    public Task<Result<int>> LatestVersionNumberAsync(Guid id, CancellationToken ct) =>
        db.ScalarAsync<int>("select coalesce(max(version), 0) from workflow.definitions where id = @id", new { id }, ct).Map(v => v);

    public Task<Result<Unit>> InsertDefinitionAsync(DbSession session, WorkflowDefinition d, Guid tenantId, CancellationToken ct) =>
        session.ExecuteAsync(
                """
                insert into workflow.definitions (id, version, tenant_id, name, status, origin, owning_module, owner_id, definition, created_at, updated_at)
                values (@id, @version, @tenantId, @name, @status, @origin, @owningModule, @ownerId, @definition::jsonb, @now, @now)
                """,
                new
                {
                    id = d.Id,
                    version = d.Version,
                    tenantId,
                    name = d.Name,
                    status = d.Status,
                    origin = d.Origin,
                    owningModule = d.OwningModule,
                    ownerId = d.OwnerId,
                    definition = JsonSerializer.Serialize(d, Json),
                    now = d.UpdatedAt ?? DateTimeOffset.UtcNow,
                }, ct)
            .BindAsync(_ => InsertDependenciesAsync(session, d, ct));

    /// <summary>Updates status and the stored JSON of an existing version (status is metadata; triggers can be suspended).</summary>
    public Task<Result<Unit>> UpdateDefinitionAsync(DbSession session, WorkflowDefinition d, CancellationToken ct) =>
        session.ExecuteExpectingAsync(
            "update workflow.definitions set status = @status, definition = @definition::jsonb, updated_at = now() where id = @id and version = @version",
            new { id = d.Id, version = d.Version, status = d.Status, definition = JsonSerializer.Serialize(d, Json) }, 1, NotFound(d.Id), ct);

    private async Task<Result<Unit>> InsertDependenciesAsync(DbSession session, WorkflowDefinition d, CancellationToken ct)
    {
        foreach (var dependency in d.Dependencies)
        {
            var result = await session.ExecuteAsync(
                """
                insert into workflow.dependencies (workflow_id, version, kind, dep_id, dep_version, steps)
                values (@id, @version, @kind, @depId, @depVersion, @steps)
                """,
                new { id = d.Id, version = d.Version, kind = dependency.Kind, depId = dependency.Id, depVersion = dependency.Version, steps = dependency.UsedBySteps.ToArray() }, ct);
            if (result.IsFailure)
            {
                return result.ToUnit();
            }
        }

        return Result.Success();
    }

    /// <summary>Latest versions of all workflows that depend on any of the given IDs (dependency index).</summary>
    public Task<Result<IReadOnlyList<WorkflowDefinition>>> DependingOnAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        db.QueryAsync(
            """
            select distinct on (d.id) d.definition::text as definition, d.status from workflow.definitions d
            where d.status <> 'Archived' and exists (
                select 1 from workflow.dependencies x where x.workflow_id = d.id and x.version = d.version and lower(x.dep_id) = any(@ids))
            order by d.id, d.version desc
            """, MapDefinition, new { ids = ids.Select(i => i.ToLowerInvariant()).ToArray() }, ct);

    // ------------------------------------------------------------ instances

    public Task<Result<WorkflowInstance>> FindInstanceAsync(Guid id, CancellationToken ct) =>
        db.FindAsync("select instance::text as instance from workflow.instances where id = @id", MapInstance, new { id },
            Error.NotFound("workflow.instance.notFound", "workflow:instance.notFound").WithMetadata("id", id), ct);

    public Task<Result<IReadOnlyList<WorkflowInstance>>> InstancesAsync(Guid tenantId, string? state, Guid? workflowId, Guid? runAs, int take, CancellationToken ct) =>
        db.QueryAsync(
            """
            select instance::text as instance from workflow.instances
            where tenant_id = @tenantId and (@state::text is null or state = @state) and (@workflowId::uuid is null or workflow_id = @workflowId)
              and (@runAs::uuid is null or run_as = @runAs)
            order by updated_at desc limit @take
            """, MapInstance, new { tenantId, state, workflowId, runAs, take }, ct);

    /// <summary>Instances waiting for a person: own input steps, or approvals the user may decide.</summary>
    public Task<Result<IReadOnlyList<WorkflowInstance>>> WaitingAsync(Guid tenantId, CancellationToken ct) =>
        db.QueryAsync(
            """
            select instance::text as instance from workflow.instances
            where tenant_id = @tenantId and state in ('WaitingForInput', 'WaitingForApproval') order by updated_at
            """, MapInstance, new { tenantId }, ct);

    public Task<Result<int>> CountRunningAsync(Guid workflowId, CancellationToken ct) =>
        db.ScalarAsync<long>(
            "select count(*) from workflow.instances where workflow_id = @workflowId and state not in ('Completed', 'Failed', 'Cancelled', 'Compensated')",
            new { workflowId }, ct).Map(v => (int)v);

    public Task<Result<IReadOnlyList<WorkflowInstance>>> RunningOfAsync(Guid workflowId, CancellationToken ct) =>
        db.QueryAsync(
            "select instance::text as instance from workflow.instances where workflow_id = @workflowId and state not in ('Completed', 'Failed', 'Cancelled', 'Compensated')",
            MapInstance, new { workflowId }, ct);

    /// <summary>Instances due for maintenance: retries, wait timeouts.</summary>
    public Task<Result<IReadOnlyList<WorkflowInstance>>> DueAsync(DateTimeOffset now, CancellationToken ct) =>
        db.QueryAsync(
            """
            select instance::text as instance from workflow.instances
            where (retry_at is not null and retry_at <= @now and state = 'Running')
               or (wait_until is not null and wait_until <= @now and state = 'WaitingForEvent')
            limit 50
            """, MapInstance, new { now }, ct);

    public Task<Result<IReadOnlyList<WorkflowInstance>>> WaitingForEventAsync(string eventType, CancellationToken ct) =>
        db.QueryAsync("select instance::text as instance from workflow.instances where state = 'WaitingForEvent' and waiting_event = @eventType",
            MapInstance, new { eventType }, ct);

    public Task<Result<Unit>> SaveInstanceAsync(DbSession session, WorkflowInstance i, Guid tenantId, InstanceColumns columns, CancellationToken ct) =>
        session.ExecuteAsync(
            """
            insert into workflow.instances (id, tenant_id, workflow_id, workflow_version, workflow_name, state, current_step_id, executor, run_as, run_as_name,
                approver_permission, waiting_event, wait_until, retry_at, attempts, parent_instance_id, instance, created_at, updated_at)
            values (@id, @tenantId, @workflowId, @version, @name, @state, @step, @executor, @runAs, @runAsName,
                @approver, @waitingEvent, @waitUntil, @retryAt, @attempts, @parent, @instance::jsonb, @createdAt, @updatedAt)
            on conflict (id) do update set state = excluded.state, current_step_id = excluded.current_step_id, executor = excluded.executor,
                approver_permission = excluded.approver_permission, waiting_event = excluded.waiting_event, wait_until = excluded.wait_until,
                retry_at = excluded.retry_at, attempts = excluded.attempts, instance = excluded.instance, updated_at = excluded.updated_at
            """,
            new
            {
                id = i.Id,
                tenantId,
                workflowId = i.WorkflowId,
                version = i.WorkflowVersion,
                name = i.WorkflowName,
                state = i.State,
                step = i.CurrentStepId,
                executor = i.Executor,
                runAs = i.RunAs,
                runAsName = i.RunAsName ?? string.Empty,
                approver = columns.ApproverPermission,
                waitingEvent = columns.WaitingEvent,
                waitUntil = columns.WaitUntil,
                retryAt = columns.RetryAt,
                attempts = columns.Attempts,
                parent = columns.ParentInstanceId,
                instance = JsonSerializer.Serialize(i, Json),
                createdAt = i.CreatedAt,
                updatedAt = i.UpdatedAt,
            }, ct).ToUnit();

    public Task<Result<InstanceColumns>> ColumnsAsync(Guid id, CancellationToken ct) =>
        db.FindAsync("select approver_permission, waiting_event, wait_until, retry_at, attempts, parent_instance_id from workflow.instances where id = @id",
            r => new InstanceColumns
            {
                ApproverPermission = r.GetOrNull<string>("approver_permission"),
                WaitingEvent = r.GetOrNull<string>("waiting_event"),
                WaitUntil = r.GetTimeOrNull("wait_until"),
                RetryAt = r.GetTimeOrNull("retry_at"),
                Attempts = r.Get<int>("attempts"),
                ParentInstanceId = r.GetNullable<Guid>("parent_instance_id"),
            }, new { id }, Error.NotFound("workflow.instance.notFound", "workflow:instance.notFound"), ct);

    // ------------------------------------------------------------ triggers

    /// <summary>Records a trigger run; false if the de-duplication key was already used.</summary>
    public Task<Result<bool>> TryRecordTriggerRunAsync(Guid workflowId, string triggerId, string dedupKey, Guid instanceId, CancellationToken ct) =>
        db.ExecuteAsync(
            """
            insert into workflow.trigger_runs (workflow_id, trigger_id, dedup_key, instance_id) values (@workflowId, @triggerId, @dedupKey, @instanceId)
            on conflict do nothing
            """, new { workflowId, triggerId, dedupKey, instanceId }, ct).Map(rows => rows == 1);

    public Task<Result<Option<DateTimeOffset>>> LastScheduleRunAsync(Guid workflowId, string triggerId, CancellationToken ct) =>
        db.QuerySingleAsync("select last_run_at from workflow.schedule_state where workflow_id = @workflowId and trigger_id = @triggerId",
            r => r.GetTime("last_run_at"), new { workflowId, triggerId }, ct);

    public Task<Result<int>> SetScheduleRunAsync(Guid workflowId, string triggerId, DateTimeOffset at, CancellationToken ct) =>
        db.ExecuteAsync(
            """
            insert into workflow.schedule_state (workflow_id, trigger_id, last_run_at) values (@workflowId, @triggerId, @at)
            on conflict (workflow_id, trigger_id) do update set last_run_at = excluded.last_run_at
            """, new { workflowId, triggerId, at }, ct);

    // ------------------------------------------------------------ impact reports

    public Task<Result<Unit>> SaveReportAsync(DbSession session, WorkflowImpactReport report, CancellationToken ct) =>
        session.ExecuteAsync(
            """
            insert into workflow.impact_reports (id, module_id, from_version, to_version, analyzed_at, report)
            values (@id, @moduleId, @fromVersion, @toVersion, @analyzedAt, @report::jsonb)
            """,
            new
            {
                id = report.Id,
                moduleId = report.ModuleId,
                fromVersion = report.FromVersion,
                toVersion = report.ToVersion,
                analyzedAt = report.AnalyzedAt,
                report = JsonSerializer.Serialize(report, Json),
            }, ct).ToUnit();

    public Task<Result<IReadOnlyList<WorkflowImpactReport>>> ReportsAsync(CancellationToken ct) =>
        db.QueryAsync("select report::text as report from workflow.impact_reports order by analyzed_at desc limit 50",
            r => JsonSerializer.Deserialize<WorkflowImpactReport>(r.Get<string>("report"), Json)!, null, ct);

    public Task<Result<WorkflowImpactReport>> FindReportAsync(Guid id, CancellationToken ct) =>
        db.FindAsync("select report::text as report from workflow.impact_reports where id = @id",
            r => JsonSerializer.Deserialize<WorkflowImpactReport>(r.Get<string>("report"), Json)!, new { id },
            Error.NotFound("workflow.impact.notFound", "workflow:impact.notFound"), ct);

    public static WorkflowSummary Summarize(WorkflowDefinition d, int running) => new(d.Id, d.Version, d.Name, d.Status)
    {
        Description = d.Description,
        Origin = d.Origin,
        OwningModule = d.OwningModule,
        AiAutonomy = d.AiPolicy.Autonomy,
        TriggerKinds = d.Triggers.Select(t => t.Kind.ToString()).Distinct().ToList(),
        RunningInstances = running,
        UpdatedAt = d.UpdatedAt ?? DateTimeOffset.MinValue,
    };

    private static Error NotFound(Guid id) => Error.NotFound("workflow.definition.notFound", "workflow:definition.notFound").WithMetadata("id", id);

    private static WorkflowDefinition MapDefinition(System.Data.Common.DbDataReader r) =>
        JsonSerializer.Deserialize<WorkflowDefinition>(r.Get<string>("definition"), Json)! with { Status = r.GetEnum<WorkflowStatus>("status") };

    private static WorkflowInstance MapInstance(System.Data.Common.DbDataReader r) =>
        JsonSerializer.Deserialize<WorkflowInstance>(r.Get<string>("instance"), Json)!;
}

/// <summary>Query columns of an instance that are not part of the WorkflowInstance contract.</summary>
public sealed record InstanceColumns
{
    public string? ApproverPermission { get; init; }

    public string? WaitingEvent { get; init; }

    public DateTimeOffset? WaitUntil { get; init; }

    public DateTimeOffset? RetryAt { get; init; }

    public int Attempts { get; init; }

    public Guid? ParentInstanceId { get; init; }
}
