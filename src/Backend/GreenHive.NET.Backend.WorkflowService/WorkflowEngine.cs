using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Events;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>How a run starts.</summary>
public sealed record StartContext(Guid TenantId, Guid RunAs, string RunAsName, WorkflowExecutor Executor, string TriggeredBy)
{
    public JsonObject? Variables { get; init; }

    public JsonObject? Payload { get; init; }

    public Guid? ParentInstanceId { get; init; }
}

/// <summary>Who completes a step: a person in the app or the AI agent (with reasoning and confidence).</summary>
public sealed record StepActor(GreenHiveUser User, ActorKind Kind)
{
    public string? Reasoning { get; init; }

    public double? Confidence { get; init; }

    public string Name => Kind == ActorKind.Ai ? "ai-agent" : User.DisplayName;
}

/// <summary>
/// Durable, headless workflow execution. The instance is persisted after every step (with its events in the same
/// transaction), so it can resume after a crash, on another device or by another executor.
/// Reaction to failed steps by error kind: Validation -> back to the human/AI with field errors, Forbidden -> stop,
/// Unavailable/Timeout -> retry with backoff, everything else -> compensation and failure.
/// </summary>
public sealed class WorkflowEngine(
    WorkflowRepository repository,
    DefinitionCache definitions,
    Db db,
    StepExecutor executor,
    AiDispatcher ai,
    OutboxWriter outbox,
    TimeProvider time,
    ILogger<WorkflowEngine> logger)
{
    private const int MaxStepsPerRun = 200;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    // ================================================================ public operations

    public async Task<Result<WorkflowInstance>> StartAsync(WorkflowDefinition definition, StartContext start, CancellationToken ct)
    {
        if (definition.Status != WorkflowStatus.Active)
        {
            return Error.Conflict("workflow.start.notActive", "workflow:start.notActive").WithMetadata("status", definition.Status.ToString());
        }

        var now = time.GetUtcNow();
        var variables = new JsonObject();
        foreach (var (key, value) in start.Variables ?? [])
        {
            variables[key] = value?.DeepClone();
        }

        var instance = new WorkflowInstance
        {
            WorkflowId = definition.Id,
            WorkflowVersion = definition.Version,
            WorkflowName = definition.Name,
            TriggeredBy = start.TriggeredBy,
            TriggerPayload = start.Payload?.DeepClone().AsObject(),
            RunAs = start.RunAs,
            RunAsName = start.RunAsName,
            Executor = start.Executor,
            State = WorkflowInstanceState.Running,
            CurrentStepId = definition.StartStepId,
            Variables = variables,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var run = new Run(instance, definition, new InstanceColumns { ParentInstanceId = start.ParentInstanceId }, start.TenantId);
        run.Events.Add(Event(run, EventTypes.WorkflowInstanceStarted, NotificationAudience.ForUser(start.RunAs), null));
        logger.LogInformation("Workflow '{Workflow}' v{Version} started for {User} ({Trigger}, executor {Executor})",
            definition.Name, definition.Version, start.RunAsName, start.TriggeredBy, start.Executor);
        return await RunAndSaveAsync(run, ct);
    }

    /// <summary>Completes the current interactive step (or decides an AI proposal) and continues the run.</summary>
    public Task<Result<WorkflowInstance>> CompleteAsync(Guid instanceId, CompleteStepRequest request, StepActor actor, CancellationToken ct) =>
        WithInstanceAsync(instanceId, ct, async run =>
        {
            var instance = run.Instance;
            if (!string.Equals(instance.CurrentStepId, request.StepId, StringComparison.OrdinalIgnoreCase))
            {
                return Error.Conflict("workflow.step.notCurrent", "workflow:step.notCurrent").WithMetadata("current", instance.CurrentStepId);
            }

            if (instance.State is not (WorkflowInstanceState.WaitingForInput or WorkflowInstanceState.WaitingForApproval))
            {
                return Error.Conflict("workflow.step.notWaiting", "workflow:step.notWaiting").WithMetadata("state", instance.State.ToString());
            }

            var step = run.Step;
            if (step is null)
            {
                return Error.Unexpected("workflow.step.missing", "workflow:step.missing");
            }

            var allowed = Authorize(run, step, actor);
            if (allowed.IsFailure)
            {
                return allowed.Error;
            }

            // Decision on an AI proposal.
            if (instance.PendingProposal is { } proposal && actor.Kind == ActorKind.Human)
            {
                return await DecideProposalAsync(run, step, proposal, request, actor, ct);
            }

            // A headless step that failed with a validation error: retry with corrected parameters.
            if (step.Kind is WorkflowStepKind.ModuleStep or WorkflowStepKind.Command && instance.LastError is not null)
            {
                run.Instance = instance with { State = WorkflowInstanceState.Running, LastError = null };
                return await ExecuteHeadlessAsync(run, step, request.Outputs, approved: true, ct);
            }

            var outputs = BuildOutputs(step, request, actor);
            if (!outputs.TryGetValue(out var values, out var invalid))
            {
                return invalid;
            }

            Record(run, step, actor.Kind == ActorKind.Ai ? "ai-agent" : actor.User.DisplayName, actor.Kind, null, values,
                step.Kind == WorkflowStepKind.Approval
                    ? string.Equals(request.Decision, "reject", StringComparison.OrdinalIgnoreCase) ? StepOutcome.Rejected : StepOutcome.Approved
                    : StepOutcome.Succeeded,
                actor.Reasoning, actor.Confidence);
            if (step.Kind == WorkflowStepKind.Approval)
            {
                run.Events.Add(Event(run, EventTypes.WorkflowApprovalDecided, NotificationAudience.ForUser(instance.RunAs),
                    "workflow:notification.approvalDecided", NotificationSeverity.Info, values));
            }

            Advance(run, step, values);
            return await RunAndSaveAsync(run, ct);
        });

    /// <summary>The AI proposes values for the current step; a person must approve them (policy ApproveEachStep / Critical).</summary>
    public Task<Result<WorkflowInstance>> ProposeAsync(Guid instanceId, AiProposalRequest request, CancellationToken ct) =>
        WithInstanceAsync(instanceId, ct, async run =>
        {
            if (!string.Equals(run.Instance.CurrentStepId, request.StepId, StringComparison.OrdinalIgnoreCase) || run.Step is not { } step)
            {
                return Error.Conflict("workflow.step.notCurrent", "workflow:step.notCurrent");
            }

            var approver = run.Definition.AiPolicy.Approvers.FirstOrDefault() ?? "ai.approve";
            var proposal = new JsonObject
            {
                ["outputs"] = request.Outputs.DeepClone(),
                ["reasoning"] = request.Reasoning,
                ["confidence"] = request.Confidence,
                ["decision"] = request.Decision,
            };
            Record(run, step, "ai-agent", ActorKind.Ai, null, request.Outputs, StepOutcome.Proposed, request.Reasoning, request.Confidence);
            run.Instance = run.Instance with { State = WorkflowInstanceState.WaitingForApproval, PendingProposal = proposal };
            run.Columns = run.Columns with { ApproverPermission = approver };
            run.Events.Add(Event(run, EventTypes.WorkflowApprovalRequested, NotificationAudience.WithPermission(approver),
                "workflow:notification.aiProposal", NotificationSeverity.Info));
            return await SaveAsync(run, ct);
        });

    /// <summary>Hands the instance over between a person and the AI agent.</summary>
    public Task<Result<WorkflowInstance>> HandOverAsync(Guid instanceId, HandOverRequest request, GreenHiveUser? user, CancellationToken ct) =>
        WithInstanceAsync(instanceId, ct, async run =>
        {
            if (run.Instance.IsFinished)
            {
                return Error.Conflict("workflow.instance.finished", "workflow:instance.finished");
            }

            if (user is not null && user.Id != run.Instance.RunAs && !user.Has("workflows.edit"))
            {
                return Error.Forbidden("workflow.handover.forbidden", "auth:permissionMissing");
            }

            if (request.Executor == WorkflowExecutor.Ai && run.Definition.AiPolicy.Autonomy == AiAutonomy.Off)
            {
                return Error.Conflict("workflow.ai.off", "workflow:ai.disabledForWorkflow");
            }

            run.Instance = run.Instance with { Executor = request.Executor, LastError = request.Reason ?? run.Instance.LastError };
            if (request.Executor == WorkflowExecutor.Ai && run.Step is { } step && run.Instance.State == WorkflowInstanceState.WaitingForInput &&
                run.Definition.AiPolicy.AllowsStep(step))
            {
                run.AiRequests.Add(step.Id);
            }

            if (request.Executor != WorkflowExecutor.Ai)
            {
                run.Events.Add(Event(run, EventTypes.WorkflowInstanceWaiting, NotificationAudience.ForUser(run.Instance.RunAs),
                    "workflow:notification.handedOver", NotificationSeverity.Warning));
            }

            return await SaveAsync(run, ct);
        });

    public Task<Result<WorkflowInstance>> CancelAsync(Guid instanceId, GreenHiveUser user, CancellationToken ct) =>
        WithInstanceAsync(instanceId, ct, async run =>
        {
            if (user.Id != run.Instance.RunAs && !user.Has("workflows.edit"))
            {
                return Error.Forbidden("workflow.cancel.forbidden", "auth:permissionMissing");
            }

            if (run.Instance.IsFinished)
            {
                return Error.Conflict("workflow.instance.finished", "workflow:instance.finished");
            }

            run.Instance = run.Instance with { State = WorkflowInstanceState.Cancelled, PendingProposal = null };
            run.Columns = run.Columns with { RetryAt = null, WaitUntil = null, WaitingEvent = null };
            return await SaveAsync(run, ct);
        });

    /// <summary>Pauses or resumes an instance (impact analysis of a module update).</summary>
    public Task<Result<WorkflowInstance>> PauseAsync(Guid instanceId, string reason, CancellationToken ct) =>
        WithInstanceAsync(instanceId, ct, async run =>
        {
            if (run.Instance.IsFinished)
            {
                return run.Instance;
            }

            run.Instance = run.Instance with { State = WorkflowInstanceState.Paused, LastError = reason };
            return await SaveAsync(run, ct);
        });

    public Task<Result<WorkflowInstance>> ResumeAsync(Guid instanceId, GreenHiveUser user, CancellationToken ct) =>
        WithInstanceAsync(instanceId, ct, async run =>
        {
            if (run.Instance.State != WorkflowInstanceState.Paused)
            {
                return Error.Conflict("workflow.instance.notPaused", "workflow:instance.notPaused");
            }

            if (user.Id != run.Instance.RunAs && !user.Has("workflows.edit"))
            {
                return Error.Forbidden("workflow.resume.forbidden", "auth:permissionMissing");
            }

            run.Instance = run.Instance with { State = WorkflowInstanceState.Running, LastError = null };
            return await RunAndSaveAsync(run, ct);
        });

    /// <summary>Resumes instances waiting for this event (WaitForEvent steps and parents of sub-workflows).</summary>
    public async Task ResumeOnEventAsync(NotificationEnvelope envelope, CancellationToken ct)
    {
        var waiting = await repository.WaitingForEventAsync(envelope.Type, ct);
        foreach (var candidate in waiting.GetValueOrDefault([]))
        {
            await WithInstanceAsync(candidate.Id, ct, async run =>
            {
                if (run.Instance.State != WorkflowInstanceState.WaitingForEvent || run.Step is not { } step)
                {
                    return run.Instance;
                }

                var payload = envelope.Payload ?? [];
                var scope = WorkflowExpression.Scope(run.Instance.Variables, run.Instance.StepOutputs, null, payload);
                if (!WorkflowExpression.EvaluateCondition(step.EventFilter, scope).GetValueOrDefault(false))
                {
                    return run.Instance;
                }

                var outputs = payload.DeepClone().AsObject();
                outputs["timeout"] = false;
                Record(run, step, "workflow-engine", ActorKind.Engine, null, outputs, StepOutcome.Succeeded, null, null);
                run.Columns = run.Columns with { WaitingEvent = null, WaitUntil = null };
                run.Instance = run.Instance with { State = WorkflowInstanceState.Running };
                Advance(run, step, outputs);
                return await RunAndSaveAsync(run, ct);
            });
        }
    }

    /// <summary>Retries of failed headless steps and timeouts of waiting steps.</summary>
    public async Task MaintenanceAsync(CancellationToken ct)
    {
        var due = await repository.DueAsync(time.GetUtcNow(), ct);
        foreach (var candidate in due.GetValueOrDefault([]))
        {
            await WithInstanceAsync(candidate.Id, ct, async run =>
            {
                if (run.Step is not { } step)
                {
                    return run.Instance;
                }

                if (run.Instance.State == WorkflowInstanceState.WaitingForEvent && run.Columns.WaitUntil <= time.GetUtcNow())
                {
                    var outputs = new JsonObject { ["timeout"] = true };
                    Record(run, step, "workflow-engine", ActorKind.Engine, null, outputs, StepOutcome.Skipped, "Timed out", null);
                    run.Columns = run.Columns with { WaitingEvent = null, WaitUntil = null };
                    run.Instance = run.Instance with { State = WorkflowInstanceState.Running };
                    Advance(run, step, outputs);
                    return await RunAndSaveAsync(run, ct);
                }

                if (run.Instance.State == WorkflowInstanceState.Running && run.Columns.RetryAt <= time.GetUtcNow())
                {
                    run.Columns = run.Columns with { RetryAt = null };
                    return await RunAndSaveAsync(run, ct);
                }

                return run.Instance;
            });
        }
    }

    /// <summary>Runs a headless step type as an AI tool on behalf of the RunAs user (actor ai-agent); recorded in the history.</summary>
    public Task<Result<JsonObject>> ExecuteToolAsync(Guid instanceId, string stepTypeId, JsonObject inputs, CancellationToken ct) =>
        repository.FindInstanceAsync(instanceId, ct).BindAsync(instance =>
            executor.ExecuteAsync(new WorkflowStep { Id = $"tool:{stepTypeId}", Kind = WorkflowStepKind.ModuleStep, StepTypeId = stepTypeId },
                inputs, instance.RunAs, "ai-agent", ct));

    // ================================================================ the run loop

    private sealed class Run(WorkflowInstance instance, WorkflowDefinition definition, InstanceColumns columns, Guid tenantId)
    {
        public WorkflowInstance Instance { get; set; } = instance;

        public WorkflowDefinition Definition { get; } = definition;

        public InstanceColumns Columns { get; set; } = columns;

        public Guid TenantId { get; } = tenantId;

        public List<NotificationEnvelope> Events { get; } = [];

        public List<string> AiRequests { get; } = [];

        public List<Func<CancellationToken, Task>> AfterCommit { get; } = [];

        public WorkflowStep? Step => Instance.CurrentStepId is { } id ? Definition.FindStep(id) : null;
    }

    private async Task<Result<WorkflowInstance>> RunAndSaveAsync(Run run, CancellationToken ct)
    {
        while (run.Instance.State == WorkflowInstanceState.Running && run.Columns.RetryAt is null)
        {
            if (run.Instance.StepCount >= MaxStepsPerRun)
            {
                Fail(run, "workflow.loop", "The workflow executed too many steps (loop?).");
                break;
            }

            if (run.Step is not { } step)
            {
                Fail(run, "workflow.step.missing", $"Step '{run.Instance.CurrentStepId}' does not exist.");
                break;
            }

            var result = await ExecuteStepAsync(run, step, ct);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return await SaveAsync(run, ct);
    }

    private async Task<Result<WorkflowInstance>> ExecuteStepAsync(Run run, WorkflowStep step, CancellationToken ct)
    {
        var policy = run.Definition.AiPolicy;
        var aiRuns = run.Instance.Executor == WorkflowExecutor.Ai;
        switch (step.Kind)
        {
            case WorkflowStepKind.Input or WorkflowStepKind.Choice or WorkflowStepKind.Review:
                Wait(run, WorkflowInstanceState.WaitingForInput);
                if (aiRuns && policy.AllowsStep(step))
                {
                    run.AiRequests.Add(step.Id);
                }
                else
                {
                    run.Events.Add(Event(run, EventTypes.WorkflowInstanceWaiting, NotificationAudience.ForUser(run.Instance.RunAs),
                        "workflow:notification.taskWaiting", NotificationSeverity.Info));
                }

                return run.Instance;

            case WorkflowStepKind.Approval:
                var approver = step.Approvers.FirstOrDefault() ?? "workflows.approve";
                Wait(run, WorkflowInstanceState.WaitingForApproval);
                run.Columns = run.Columns with { ApproverPermission = approver };
                run.Events.Add(Event(run, EventTypes.WorkflowApprovalRequested, NotificationAudience.WithPermission(approver),
                    "workflow:notification.approvalRequested", NotificationSeverity.Info));
                return run.Instance;

            case WorkflowStepKind.AiTask:
                Wait(run, WorkflowInstanceState.WaitingForInput);
                if (policy.Autonomy != AiAutonomy.Off)
                {
                    run.Instance = run.Instance with { Executor = aiRuns ? WorkflowExecutor.Ai : WorkflowExecutor.Mixed };
                    run.AiRequests.Add(step.Id);
                }

                return run.Instance;

            case WorkflowStepKind.WaitForEvent:
                Wait(run, WorkflowInstanceState.WaitingForEvent);
                run.Columns = run.Columns with
                {
                    WaitingEvent = step.EventType,
                    WaitUntil = step.TimeoutSeconds is { } seconds ? time.GetUtcNow().AddSeconds(seconds) : null,
                };
                return run.Instance;

            case WorkflowStepKind.SubWorkflow:
                return await StartChildAsync(run, step, ct);

            case WorkflowStepKind.Command or WorkflowStepKind.ModuleStep:
                if (aiRuns && policy.NeedsApproval(step))
                {
                    // Critical step under AI execution: a person approves the evaluated parameters first.
                    var parameters = EvaluateParameters(run, step, null);
                    if (!parameters.TryGetValue(out var values, out var error))
                    {
                        Fail(run, error.Code, error.Message);
                        return run.Instance;
                    }

                    var approverPermission = policy.Approvers.FirstOrDefault() ?? "ai.approve";
                    run.Instance = run.Instance with
                    {
                        State = WorkflowInstanceState.WaitingForApproval,
                        PendingProposal = new JsonObject
                        {
                            ["outputs"] = values,
                            ["reasoning"] = "Critical step - the AI needs an approval before it is executed.",
                            ["kind"] = "critical",
                        },
                        UpdatedAt = time.GetUtcNow(),
                    };
                    run.Columns = run.Columns with { ApproverPermission = approverPermission };
                    run.Events.Add(Event(run, EventTypes.WorkflowApprovalRequested, NotificationAudience.WithPermission(approverPermission),
                        "workflow:notification.criticalStep", NotificationSeverity.Warning));
                    return run.Instance;
                }

                return await ExecuteHeadlessAsync(run, step, null, approved: false, ct);

            default:
                Fail(run, "workflow.step.kind", $"Unsupported step kind {step.Kind}.");
                return run.Instance;
        }
    }

    private async Task<Result<WorkflowInstance>> ExecuteHeadlessAsync(Run run, WorkflowStep step, JsonObject? overrides, bool approved, CancellationToken ct)
    {
        var parameters = EvaluateParameters(run, step, overrides);
        if (!parameters.TryGetValue(out var inputs, out var parameterError))
        {
            Fail(run, parameterError.Code, parameterError.Message);
            return run.Instance;
        }

        var actor = run.Instance.Executor == WorkflowExecutor.Ai ? "ai-agent" : "workflow-engine";
        var started = time.GetUtcNow();
        var result = await executor.ExecuteAsync(step, inputs, run.Instance.RunAs, actor, ct);
        if (result.TryGetValue(out var outputs, out var error))
        {
            Record(run, step, actor, actor == "ai-agent" ? ActorKind.Ai : ActorKind.Engine, inputs, outputs, StepOutcome.Succeeded, null, null, started);
            run.Columns = run.Columns with { Attempts = 0, RetryAt = null };
            run.Instance = run.Instance with { LastError = null };
            Advance(run, step, outputs);
            return run.Instance;
        }

        Record(run, step, actor, actor == "ai-agent" ? ActorKind.Ai : ActorKind.Engine, inputs, null, StepOutcome.Failed, null, null, started, error);
        switch (error.Kind)
        {
            case ErrorKind.Validation:
                // Back to a person with the field errors (the step can be retried with corrected parameters).
                Wait(run, WorkflowInstanceState.WaitingForInput);
                run.Instance = run.Instance with { LastError = $"{error.Code}: {error.Message}", Executor = run.Instance.Executor == WorkflowExecutor.Ai ? WorkflowExecutor.Mixed : run.Instance.Executor };
                run.Events.Add(Event(run, EventTypes.WorkflowInstanceWaiting, NotificationAudience.ForUser(run.Instance.RunAs),
                    "workflow:notification.stepNeedsAttention", NotificationSeverity.Warning));
                break;
            case ErrorKind.Unavailable or ErrorKind.Timeout when run.Columns.Attempts < step.MaxRetries:
                var attempts = run.Columns.Attempts + 1;
                run.Columns = run.Columns with { Attempts = attempts, RetryAt = time.GetUtcNow().AddSeconds(5 * Math.Pow(2, attempts)) };
                run.Instance = run.Instance with { LastError = $"{error.Code} (retry {attempts}/{step.MaxRetries})" };
                logger.LogWarning("Step {Step} of {Instance} failed temporarily, retry {Attempt}", step.Id, run.Instance.Id, attempts);
                break;
            case ErrorKind.Forbidden or ErrorKind.Unauthorized:
                Fail(run, error.Code, error.Message);
                break;
            default:
                if (step.CompensationStepId is { } compensation && run.Definition.FindStep(compensation) is not null)
                {
                    run.Instance = run.Instance with { CurrentStepId = compensation, LastError = $"{error.Code}: {error.Message}" };
                    logger.LogWarning("Step {Step} failed ({Error}); running compensation {Compensation}", step.Id, error.Code, compensation);
                }
                else
                {
                    Fail(run, error.Code, error.Message);
                }

                break;
        }

        return run.Instance;
    }

    private async Task<Result<WorkflowInstance>> StartChildAsync(Run run, WorkflowStep step, CancellationToken ct)
    {
        if (step.SubWorkflowId is not { } childId)
        {
            Fail(run, "workflow.subWorkflow.missing", "The sub-workflow step has no workflow.");
            return run.Instance;
        }

        var parameters = EvaluateParameters(run, step, null);
        if (!parameters.TryGetValue(out var variables, out var error))
        {
            Fail(run, error.Code, error.Message);
            return run.Instance;
        }

        Wait(run, WorkflowInstanceState.WaitingForEvent);
        var parentId = run.Instance.Id;
        run.Columns = run.Columns with { WaitingEvent = $"workflow.child.{parentId:n}" };
        var start = new StartContext(run.TenantId, run.Instance.RunAs, run.Instance.RunAsName ?? string.Empty, run.Instance.Executor, $"subworkflow:{parentId}")
        {
            Variables = variables,
            ParentInstanceId = parentId,
        };
        run.AfterCommit.Add(async token =>
        {
            var child = await repository.FindLatestAsync(childId, token).BindAsync(definition => StartAsync(definition, start, token));
            if (child.IsFailure)
            {
                await WithInstanceAsync(parentId, token, async parent =>
                {
                    Fail(parent, child.Error.Code, child.Error.Message);
                    return await SaveAsync(parent, token);
                });
            }
        });
        return run.Instance;
    }

    /// <summary>Called when a child instance ends: the parent continues with the child's variables as outputs.</summary>
    private Task<Result<WorkflowInstance>> ResumeParentAsync(Guid parentId, WorkflowInstance child, CancellationToken ct) =>
        WithInstanceAsync(parentId, ct, async run =>
        {
            if (run.Instance.State != WorkflowInstanceState.WaitingForEvent || run.Step is not { } step)
            {
                return run.Instance;
            }

            var outputs = child.Variables.DeepClone().AsObject();
            outputs["childState"] = child.State.ToString();
            Record(run, step, "workflow-engine", ActorKind.Engine, null, outputs,
                child.State == WorkflowInstanceState.Completed ? StepOutcome.Succeeded : StepOutcome.Failed, null, null);
            run.Columns = run.Columns with { WaitingEvent = null, WaitUntil = null };
            run.Instance = run.Instance with { State = WorkflowInstanceState.Running };
            if (child.State == WorkflowInstanceState.Completed)
            {
                Advance(run, step, outputs);
            }
            else
            {
                Fail(run, "workflow.subWorkflow.failed", $"Sub-workflow ended with {child.State}.");
            }

            return await RunAndSaveAsync(run, ct);
        });

    private async Task<Result<WorkflowInstance>> DecideProposalAsync(Run run, WorkflowStep step, JsonObject proposal, CompleteStepRequest request, StepActor actor, CancellationToken ct)
    {
        var approve = !string.Equals(request.Decision, "reject", StringComparison.OrdinalIgnoreCase);
        var proposedOutputs = request.Outputs ?? proposal["outputs"]?.AsObject() ?? [];
        run.Instance = run.Instance with { PendingProposal = null, State = WorkflowInstanceState.Running };
        run.Columns = run.Columns with { ApproverPermission = null };
        Record(run, step, actor.User.DisplayName, ActorKind.Human, null, proposedOutputs, approve ? StepOutcome.Approved : StepOutcome.Rejected,
            request.Comment, null);
        run.Events.Add(Event(run, EventTypes.WorkflowApprovalDecided, NotificationAudience.ForUser(run.Instance.RunAs),
            approve ? "workflow:notification.proposalApproved" : "workflow:notification.proposalRejected", NotificationSeverity.Info));

        if (step.Kind is WorkflowStepKind.ModuleStep or WorkflowStepKind.Command)
        {
            if (approve)
            {
                await ExecuteHeadlessAsync(run, step, proposedOutputs, approved: true, ct);
            }
            else
            {
                Advance(run, step, new JsonObject { ["rejected"] = true, ["comment"] = request.Comment });
            }

            return await RunAndSaveAsync(run, ct);
        }

        if (!approve)
        {
            // A person takes over the step.
            Wait(run, WorkflowInstanceState.WaitingForInput);
            run.Instance = run.Instance with { Executor = WorkflowExecutor.Mixed };
            run.Events.Add(Event(run, EventTypes.WorkflowInstanceWaiting, NotificationAudience.ForUser(run.Instance.RunAs),
                "workflow:notification.handedOver", NotificationSeverity.Warning));
            return await SaveAsync(run, ct);
        }

        var outputs = BuildOutputs(step, request with { Outputs = proposedOutputs, Decision = request.Decision is "approve" ? proposal["decision"]?.GetValue<string>() : request.Decision }, actor);
        if (!outputs.TryGetValue(out var values, out var invalid))
        {
            return invalid;
        }

        Advance(run, step, values);
        return await RunAndSaveAsync(run, ct);
    }

    // ================================================================ helpers

    private static Result<Unit> Authorize(Run run, WorkflowStep step, StepActor actor)
    {
        if (actor.Kind == ActorKind.Ai)
        {
            return run.Instance.PendingProposal is null && (run.Definition.AiPolicy.AllowsStep(step) || step.Kind == WorkflowStepKind.AiTask)
                ? Result.Success()
                : Error.Forbidden("workflow.ai.notAllowed", "workflow:ai.stepNotAllowed");
        }

        var user = actor.User;
        if (run.Instance.State == WorkflowInstanceState.WaitingForApproval)
        {
            var permission = run.Columns.ApproverPermission ?? "workflows.approve";
            return user.Has(permission) ? Result.Success() : Error.Forbidden("workflow.approval.forbidden", "auth:permissionMissing").WithMetadata("permission", permission);
        }

        return user.Id == run.Instance.RunAs || user.Has("workflows.edit")
            ? Result.Success()
            : Error.Forbidden("workflow.step.forbidden", "workflow:step.notYourTask");
    }

    private static Result<JsonObject> BuildOutputs(WorkflowStep step, CompleteStepRequest request, StepActor actor)
    {
        var outputs = request.Outputs?.DeepClone().AsObject() ?? [];
        switch (step.Kind)
        {
            case WorkflowStepKind.Approval:
                if (request.Decision is not ("approve" or "reject"))
                {
                    return Error.Field("decision", "workflow.approval.decision", "workflow:approval.decisionRequired");
                }

                outputs["decision"] = request.Decision;
                outputs["comment"] = request.Comment ?? string.Empty;
                outputs["decidedBy"] = actor.User.UserName;
                return outputs;

            case WorkflowStepKind.Choice:
                if (request.Decision is null || !step.Options.Any(o => o.Value.Equals(request.Decision, StringComparison.OrdinalIgnoreCase)))
                {
                    return Error.Field("decision", "workflow.choice.option", "validation:oneOf");
                }

                outputs["choice"] = step.Options.First(o => o.Value.Equals(request.Decision, StringComparison.OrdinalIgnoreCase)).Value;
                return outputs;

            case WorkflowStepKind.Review:
                outputs["confirmed"] = !string.Equals(request.Decision, "reject", StringComparison.OrdinalIgnoreCase);
                outputs["comment"] = request.Comment ?? string.Empty;
                return outputs;

            default:
                return step.Input is { } schema ? InputValidator.Validate(schema, outputs).ToResult() : outputs;
        }
    }

    private static Result<JsonObject> EvaluateParameters(Run run, WorkflowStep step, JsonObject? overrides)
    {
        var scope = WorkflowExpression.Scope(run.Instance.Variables, run.Instance.StepOutputs, null, run.Instance.TriggerPayload);
        var inputs = new JsonObject();
        foreach (var (name, expression) in step.Parameters)
        {
            var value = WorkflowExpression.Evaluate(expression, scope);
            if (!value.TryGetValue(out var evaluated, out var error))
            {
                return error.WithMetadata("parameter", name);
            }

            inputs[name] = WorkflowExpression.ToNode(evaluated);
        }

        foreach (var (key, value) in overrides ?? [])
        {
            inputs[key] = value?.DeepClone();
        }

        return inputs;
    }

    /// <summary>Stores the step's outputs, maps them to variables and moves to the next step (first matching transition).</summary>
    private void Advance(Run run, WorkflowStep step, JsonObject outputs)
    {
        var instance = run.Instance;
        var stepOutputs = instance.StepOutputs.DeepClone().AsObject();
        stepOutputs[step.Id] = outputs.DeepClone();
        var variables = instance.Variables.DeepClone().AsObject();
        if (step.OutputMapping.Count > 0)
        {
            foreach (var (output, variable) in step.OutputMapping)
            {
                variables[variable] = outputs.FirstOrDefault(kv => kv.Key.Equals(output, StringComparison.OrdinalIgnoreCase)).Value?.DeepClone();
            }
        }
        else if (step.Kind is not WorkflowStepKind.Approval)
        {
            foreach (var (key, value) in outputs)
            {
                variables[key] = value?.DeepClone();
            }
        }

        run.Instance = instance with { StepOutputs = stepOutputs, Variables = variables, LastError = null };
        var scope = WorkflowExpression.Scope(variables, stepOutputs, outputs, instance.TriggerPayload);
        var transitions = run.Definition.Transitions.Where(t => t.From.Equals(step.Id, StringComparison.OrdinalIgnoreCase)).OrderBy(t => t.Order).ToList();
        if (transitions.Count == 0)
        {
            Complete(run);
            return;
        }

        foreach (var transition in transitions)
        {
            var matches = WorkflowExpression.EvaluateCondition(transition.Condition, scope);
            if (!matches.TryGetValue(out var ok, out var error))
            {
                Fail(run, error.Code, $"Condition of transition {transition.From} -> {transition.To}: {error.Message}");
                return;
            }

            if (ok)
            {
                if (transition.To.Equals(WorkflowTransition.End, StringComparison.OrdinalIgnoreCase))
                {
                    Complete(run);
                }
                else
                {
                    run.Instance = run.Instance with { CurrentStepId = transition.To, State = WorkflowInstanceState.Running };
                }

                return;
            }
        }

        Fail(run, "workflow.transition.none", $"No transition of step '{step.Id}' matched.");
    }

    private void Complete(Run run)
    {
        run.Instance = run.Instance with { State = WorkflowInstanceState.Completed, CurrentStepId = null, PendingProposal = null };
        run.Columns = run.Columns with { RetryAt = null, WaitUntil = null, WaitingEvent = null };
        run.Events.Add(Event(run, EventTypes.WorkflowInstanceCompleted, NotificationAudience.ForUser(run.Instance.RunAs),
            "workflow:notification.completed", NotificationSeverity.Success));
        NotifyParent(run);
    }

    private void Fail(Run run, string code, string message)
    {
        run.Instance = run.Instance with { State = WorkflowInstanceState.Failed, LastError = $"{code}: {message}", PendingProposal = null };
        run.Columns = run.Columns with { RetryAt = null, WaitUntil = null, WaitingEvent = null };
        run.Events.Add(Event(run, EventTypes.WorkflowInstanceFailed, NotificationAudience.ForUser(run.Instance.RunAs),
            "workflow:notification.failed", NotificationSeverity.Error));
        logger.LogWarning("Workflow instance {Instance} ({Workflow}) failed: {Code} {Message}", run.Instance.Id, run.Instance.WorkflowName, code, message);
        NotifyParent(run);
    }

    private void NotifyParent(Run run)
    {
        if (run.Columns.ParentInstanceId is { } parentId)
        {
            var child = run.Instance;
            run.AfterCommit.Add(token => ResumeParentAsync(parentId, child, token));
        }
    }

    private void Wait(Run run, WorkflowInstanceState state) =>
        run.Instance = run.Instance with { State = state, UpdatedAt = time.GetUtcNow() };

    private void Record(Run run, WorkflowStep step, string actor, ActorKind kind, JsonObject? inputs, JsonObject? outputs, StepOutcome outcome,
        string? reasoning, double? confidence, DateTimeOffset? started = null, Error? error = null)
    {
        var now = time.GetUtcNow();
        var entry = new WorkflowHistoryEntry
        {
            StepId = step.Id,
            StepKind = step.Kind,
            Actor = actor,
            ActorKind = kind,
            Inputs = inputs?.DeepClone().AsObject(),
            Outputs = outputs?.DeepClone().AsObject(),
            Outcome = outcome,
            ErrorCode = error?.Code,
            ErrorMessage = error?.Message,
            Reasoning = reasoning,
            Confidence = confidence,
            StartedAt = started ?? now,
            FinishedAt = now,
        };
        run.Instance = run.Instance with { History = [.. run.Instance.History, entry], StepCount = run.Instance.StepCount + 1, UpdatedAt = now };
    }

    private NotificationEnvelope Event(Run run, string type, NotificationAudience audience, string? title, NotificationSeverity severity = NotificationSeverity.Info,
        JsonObject? extra = null)
    {
        var payload = new JsonObject
        {
            ["instanceId"] = run.Instance.Id.ToString(),
            ["workflowId"] = run.Instance.WorkflowId.ToString(),
            ["workflowName"] = run.Instance.WorkflowName,
            ["stepId"] = run.Instance.CurrentStepId,
            ["state"] = run.Instance.State.ToString(),
        };
        foreach (var (key, value) in extra ?? [])
        {
            payload[key] = value?.DeepClone();
        }

        return outbox.Create(type, payload, audience, run.TenantId) with
        {
            Title = title,
            Message = run.Instance.WorkflowName,
            Severity = severity,
            NavigationTarget = title is null ? null : "workflow.inbox",
            NavigationParameter = run.Instance.Id.ToString(),
        };
    }

    private async Task<Result<WorkflowInstance>> SaveAsync(Run run, CancellationToken ct)
    {
        run.Instance = run.Instance with { UpdatedAt = time.GetUtcNow() };
        var saved = await db.InTransactionAsync(async session =>
        {
            var result = await repository.SaveInstanceAsync(session, run.Instance, run.TenantId, run.Columns, ct);
            foreach (var envelope in run.Events)
            {
                if (result.IsFailure)
                {
                    break;
                }

                result = await outbox.AddAsync(session, envelope, ct);
            }

            return result.Map(_ => run.Instance);
        }, ct: ct);

        if (saved.IsSuccess)
        {
            foreach (var stepId in run.AiRequests.Distinct())
            {
                var requested = await ai.RequestAsync(run.Instance.Id, stepId, ct);
                if (requested.IsFailure)
                {
                    // AI not reachable: a person takes over instead of leaving the task unattended.
                    await HandOverAsync(run.Instance.Id, new HandOverRequest(WorkflowExecutor.Mixed, "AI agent unavailable - handed over to a person."), null, ct);
                }
            }

            foreach (var action in run.AfterCommit)
            {
                await action(ct);
            }
        }

        run.Events.Clear();
        run.AiRequests.Clear();
        run.AfterCommit.Clear();
        return saved;
    }

    /// <summary>Loads an instance with its definition under an in-process lock (one change at a time per instance).</summary>
    private async Task<Result<WorkflowInstance>> WithInstanceAsync(Guid instanceId, CancellationToken ct, Func<Run, Task<Result<WorkflowInstance>>> work)
    {
        var gate = Locks.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var loaded = await repository.FindInstanceAsync(instanceId, ct)
                .BindAsync(instance => definitions.GetAsync(instance.WorkflowId, instance.WorkflowVersion, ct).Map(definition => (instance, definition)))
                .BindAsync(x => repository.ColumnsAsync(instanceId, ct).Map(columns => (x.instance, x.definition, columns)))
                .BindAsync(x => TenantOfAsync(instanceId, ct).Map(tenant => new Run(x.instance, x.definition, x.columns, tenant)));
            return await loaded.BindAsync(work);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<Result<Guid>> TenantOfAsync(Guid instanceId, CancellationToken ct) =>
        db.ScalarAsync<Guid>("select tenant_id from workflow.instances where id = @instanceId", new { instanceId }, ct).Map(v => v);
}
