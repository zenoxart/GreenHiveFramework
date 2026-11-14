using System.Text.Json.Nodes;

namespace GreenHive.NET.WorkflowContracts.Api;

// ---------------- Workflows (/api/workflows) ----------------

public sealed record WorkflowSummary(Guid Id, int Version, string Name, WorkflowStatus Status)
{
    public string? Description { get; init; }

    public WorkflowOrigin Origin { get; init; }

    public string? OwningModule { get; init; }

    public AiAutonomy AiAutonomy { get; init; }

    public IReadOnlyList<string> TriggerKinds { get; init; } = [];

    public int RunningInstances { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record SaveWorkflowRequest(WorkflowDefinition Definition, bool Publish = false);

public sealed record SaveWorkflowResponse(Guid Id, int Version, WorkflowStatus Status, IReadOnlyList<WorkflowDependency> Dependencies);

public sealed record StartWorkflowRequest(JsonObject? Variables = null, WorkflowExecutor Executor = WorkflowExecutor.Human);

/// <summary>Completes the current interactive step of an instance (form values, choice, review, approval).</summary>
public sealed record CompleteStepRequest(string StepId)
{
    public JsonObject? Outputs { get; init; }

    /// <summary>Choice value, or "approve" / "reject" for approvals and AI proposals.</summary>
    public string? Decision { get; init; }

    public string? Comment { get; init; }
}

public sealed record HandOverRequest(WorkflowExecutor Executor, string? Reason = null);

/// <summary>A task waiting for the current user (or for approval of an AI proposal).</summary>
public sealed record InboxItem(Guid InstanceId, Guid WorkflowId, string WorkflowName, string StepId, string StepTitle, WorkflowStepKind StepKind)
{
    public WorkflowInstanceState State { get; init; }

    public string? Description { get; init; }

    public InputSchema? Input { get; init; }

    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];

    /// <summary>Variables visible to the user (sensitive values masked).</summary>
    public JsonObject Variables { get; init; } = [];

    public JsonObject? Proposal { get; init; }

    public string? ProposalReasoning { get; init; }

    public double? ProposalConfidence { get; init; }

    public string? LastError { get; init; }

    public string RunAsName { get; init; } = string.Empty;

    public WorkflowExecutor Executor { get; init; }

    public DateTimeOffset Since { get; init; }
}

public sealed record InstanceSummary(Guid Id, Guid WorkflowId, string WorkflowName, WorkflowInstanceState State)
{
    public string? CurrentStepId { get; init; }

    public WorkflowExecutor Executor { get; init; }

    public string RunAsName { get; init; } = string.Empty;

    public string? TriggeredBy { get; init; }

    public string? LastError { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Instance plus the definition version it runs (internal API for the AI agent and the client history view).</summary>
public sealed record InstanceDetails(WorkflowInstance Instance, WorkflowDefinition Definition);

/// <summary>An AI proposal for the current step (stored on the instance until approved).</summary>
public sealed record AiProposalRequest(string StepId, JsonObject Outputs, string Reasoning, double Confidence)
{
    public string? Decision { get; init; }
}

/// <summary>AI completion of a step (internal; executed as actor "ai-agent").</summary>
public sealed record AiCompleteRequest(string StepId, JsonObject Outputs, string Reasoning, double Confidence)
{
    public string? Decision { get; init; }
}

// ---------------- AI agent (/api/ai) ----------------

public sealed record AiRunRequest(Guid InstanceId, string StepId);

public sealed record AiStatus(string Provider, string Model, bool Enabled)
{
    public int RunsToday { get; init; }

    public int TokensToday { get; init; }
}

public sealed record AiAuditEntry(Guid Id, Guid InstanceId, string StepId, DateTimeOffset At, string Decision)
{
    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string? Reasoning { get; init; }

    public double? Confidence { get; init; }

    public string? PolicyCode { get; init; }

    public JsonObject? Proposal { get; init; }

    public int Tokens { get; init; }
}

public sealed record AiSwitchRequest(bool Enabled);
