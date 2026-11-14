using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>Saves a new version of a definition: validate -> authorize -> compute dependencies -> persist (published versions are immutable).</summary>
public sealed class SaveWorkflow(WorkflowRepository repository, ModuleDirectory modules, TriggerEngine triggers, Db db, AuditLog audit, RequestContext context)
{
    public Task<Result<SaveWorkflowResponse>> ExecuteAsync(SaveWorkflowRequest request, CancellationToken ct) =>
        Steps.Authorize(context.User, "workflows.edit")
            .Bind(_ => request.Definition is null
                ? Result.Failure<WorkflowDefinition>(Error.Field("definition", "workflow.definition.missing", "validation:required"))
                : WorkflowAnalysis.Validate(request.Definition).ToResult())
            .BindAsync(async definition =>
            {
                var latest = await repository.LatestVersionNumberAsync(definition.Id, ct);
                var existing = latest.GetValueOrDefault(0) > 0 ? (await repository.FindLatestAsync(definition.Id, ct)).GetValueOrDefault(null!) : null;
                var moduleOf = await modules.ModuleOfAsync(ct);
                var versionOf = await modules.VersionOfAsync(ct);
                var next = definition with
                {
                    Version = latest.GetValueOrDefault(0) + 1,
                    Status = request.Publish ? WorkflowStatus.Active : WorkflowStatus.Draft,
                    Origin = existing?.Origin == WorkflowOrigin.ModuleTemplate ? WorkflowOrigin.Tenant : definition.Origin,
                    OwnerId = existing?.OwnerId ?? definition.OwnerId ?? context.User.Id,
                    UpdatedAt = context.Now,
                    Triggers = definition.Triggers.Select(t => t with { Suspended = false, SuspendedReason = null }).ToList(),
                };
                next = next with { Dependencies = WorkflowAnalysis.ComputeDependencies(next, moduleOf, versionOf) };
                return await db.InTransactionAsync(session => repository.InsertDefinitionAsync(session, next, context.User.TenantId, ct)
                        .BindAsync(_ => audit.WriteAsync(session, context.User, request.Publish ? "workflow.published" : "workflow.saved",
                            "workflow", $"{next.Id}@{next.Version}", ct: ct)), ct: ct)
                    .Tap(_ => triggers.Invalidate())
                    .Map(_ => new SaveWorkflowResponse(next.Id, next.Version, next.Status, next.Dependencies));
            });
}

/// <summary>Starts a workflow manually.</summary>
public sealed class StartWorkflow(WorkflowRepository repository, WorkflowEngine engine, RequestContext context)
{
    public Task<Result<WorkflowInstance>> ExecuteAsync(Guid workflowId, StartWorkflowRequest request, CancellationToken ct) =>
        Steps.Authorize(context.User, "workflows.run")
            .Bind(_ => request.Executor == WorkflowExecutor.Ai ? Steps.Authorize(context.User, "ai.run") : Result.Success())
            .BindAsync(_ => repository.FindLatestAsync(workflowId, ct))
            .Ensure(d => d.Permissions.Start.Count == 0 || d.Permissions.Start.Any(context.User.Has),
                Error.Forbidden("workflow.start.forbidden", "auth:permissionMissing"))
            .Ensure(d => request.Executor != WorkflowExecutor.Ai || d.AiPolicy.Autonomy != AiAutonomy.Off,
                Error.Conflict("workflow.ai.off", "workflow:ai.disabledForWorkflow"))
            .BindAsync(d => engine.StartAsync(d, new StartContext(context.User.TenantId, context.User.Id, context.User.DisplayName, request.Executor, "manual")
            {
                Variables = request.Variables,
            }, ct));
}

/// <summary>The tasks waiting for the signed-in user: own input steps (not assigned to the AI) and approvals they may decide.</summary>
public sealed class GetInbox(DefinitionCache definitions, Db db, RequestContext context)
{
    public Task<Result<IReadOnlyList<InboxItem>>> ExecuteAsync(CancellationToken ct) =>
        Steps.Authenticated(context.User).BindAsync(user => db.QueryAsync(
                """
                select instance::text as instance, approver_permission from workflow.instances
                where tenant_id = @tenant and state in ('WaitingForInput', 'WaitingForApproval') order by updated_at
                """,
                r => (Json: r.Get<string>("instance"), Approver: r.GetOrNull<string>("approver_permission")), new { tenant = user.TenantId }, ct))
            .BindAsync(async rows =>
            {
                var items = new List<InboxItem>();
                foreach (var row in rows)
                {
                    var instance = System.Text.Json.JsonSerializer.Deserialize<WorkflowInstance>(row.Json, GreenHive.NET.Contracts.GreenHiveJson.Options)!;
                    var definition = await definitions.GetAsync(instance.WorkflowId, instance.WorkflowVersion, ct);
                    if (!definition.TryGetValue(out var d) || instance.CurrentStepId is null || d.FindStep(instance.CurrentStepId) is not { } step)
                    {
                        continue;
                    }

                    var visible = instance.State == WorkflowInstanceState.WaitingForApproval
                        ? context.User.Has(row.Approver ?? "workflows.approve")
                        : instance.RunAs == context.User.Id && !AssignedToAi(instance, d, step);
                    if (visible)
                    {
                        items.Add(ToItem(instance, d, step));
                    }
                }

                return Result.Success<IReadOnlyList<InboxItem>>(items);
            });

    public static bool AssignedToAi(WorkflowInstance instance, WorkflowDefinition definition, WorkflowStep step) =>
        instance.PendingProposal is null && instance.LastError is null &&
        ((instance.Executor == WorkflowExecutor.Ai && definition.AiPolicy.AllowsStep(step)) ||
         (step.Kind == WorkflowStepKind.AiTask && definition.AiPolicy.Autonomy != AiAutonomy.Off && instance.Executor != WorkflowExecutor.Human));

    public static InboxItem ToItem(WorkflowInstance instance, WorkflowDefinition definition, WorkflowStep step) =>
        new(instance.Id, instance.WorkflowId, instance.WorkflowName, step.Id, step.Title, step.Kind)
        {
            State = instance.State,
            Description = step.Description,
            Input = step.Input,
            Options = step.Options,
            Variables = InputValidator.Mask(instance.Variables, definition.IsSensitive),
            Proposal = instance.PendingProposal?["outputs"]?.DeepClone().AsObject(),
            ProposalReasoning = instance.PendingProposal?["reasoning"]?.GetValue<string>(),
            ProposalConfidence = instance.PendingProposal?["confidence"] is JsonValue c && c.TryGetValue<double>(out var confidence) ? confidence : null,
            LastError = instance.LastError,
            RunAsName = instance.RunAsName ?? string.Empty,
            Executor = instance.Executor,
            Since = instance.UpdatedAt,
        };
}
