using System.Text.Json.Nodes;

namespace GreenHive.NET.WorkflowContracts;

public enum WorkflowInstanceState
{
    Running,
    WaitingForInput,
    WaitingForApproval,
    WaitingForEvent,
    Paused,
    Completed,
    Failed,
    Cancelled,
    Compensated,
}

public enum ActorKind
{
    Human,
    Ai,
    Engine,
}

public enum StepOutcome
{
    Succeeded,
    Failed,
    Skipped,
    Approved,
    Rejected,
    Proposed,
}

/// <summary>One executed step - append-only audit trail of an instance.</summary>
public sealed record WorkflowHistoryEntry
{
    public required string StepId { get; init; }

    public WorkflowStepKind StepKind { get; init; }

    public required string Actor { get; init; }

    public ActorKind ActorKind { get; init; }

    public JsonObject? Inputs { get; init; }

    public JsonObject? Outputs { get; init; }

    public StepOutcome Outcome { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>Short reasoning summary of the AI (never the full prompt).</summary>
    public string? Reasoning { get; init; }

    public double? Confidence { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }
}

/// <summary>A running or finished execution of a workflow definition version. Persisted after every step.</summary>
public sealed record WorkflowInstance
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid WorkflowId { get; init; }

    public int WorkflowVersion { get; init; } = 1;

    public string WorkflowName { get; init; } = string.Empty;

    public string? TriggeredBy { get; init; }

    public JsonObject? TriggerPayload { get; init; }

    public required Guid RunAs { get; init; }

    public string? RunAsName { get; init; }

    public WorkflowExecutor Executor { get; init; } = WorkflowExecutor.Human;

    public WorkflowInstanceState State { get; init; } = WorkflowInstanceState.Running;

    public string? CurrentStepId { get; init; }

    public JsonObject Variables { get; init; } = [];

    /// <summary>Outputs per step ID (readable in expressions as steps.&lt;id&gt;.&lt;output&gt;).</summary>
    public JsonObject StepOutputs { get; init; } = [];

    public IReadOnlyList<WorkflowHistoryEntry> History { get; init; } = [];

    /// <summary>A pending AI proposal waiting for human approval (ApproveEachStep / Critical).</summary>
    public JsonObject? PendingProposal { get; init; }

    public string? LastError { get; init; }

    public int StepCount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsFinished => State is WorkflowInstanceState.Completed or WorkflowInstanceState.Failed
        or WorkflowInstanceState.Cancelled or WorkflowInstanceState.Compensated;
}
