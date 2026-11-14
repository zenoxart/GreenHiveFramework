namespace GreenHive.NET.WorkflowContracts;

public enum WorkflowTriggerKind
{
    Manual,
    Command,
    Event,
    Schedule,
    Condition,
}

public enum WorkflowExecutor
{
    Human,
    Ai,
    Mixed,
}

/// <summary>Defines how a workflow run starts and who executes it.</summary>
public sealed record WorkflowTrigger
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n")[..8];

    public required WorkflowTriggerKind Kind { get; init; }

    /// <summary>Event triggers: event type ("documents.invoice.submitted").</summary>
    public string? EventType { get; init; }

    /// <summary>Command triggers: command ID.</summary>
    public string? CommandId { get; init; }

    /// <summary>Filter expression over "payload" (event) - e.g. payload.category == 'invoice'.</summary>
    public string? Filter { get; init; }

    /// <summary>Schedule triggers: interval in minutes (simple schedule) or a cron-like "HH:mm" daily time.</summary>
    public string? Schedule { get; init; }

    /// <summary>Condition triggers: module API (relative to the gateway) returning a JSON boolean, and the polling interval.</summary>
    public string? ConditionUrl { get; init; }

    public int IntervalSeconds { get; init; } = 300;

    /// <summary>Variable -> expression over "payload".</summary>
    public IReadOnlyDictionary<string, string> PayloadMapping { get; init; } = new Dictionary<string, string>();

    public WorkflowExecutor Executor { get; init; } = WorkflowExecutor.Human;

    /// <summary>The user on whose behalf the run executes (the AI never gets more rights).</summary>
    public Guid? RunAs { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>Set by the impact analysis: the trigger is skipped until the workflow was reviewed.</summary>
    public bool Suspended { get; init; }

    public string? SuspendedReason { get; init; }

    /// <summary>Expression over "payload" that identifies duplicates (e.g. payload.invoiceId).</summary>
    public string? DeduplicationKey { get; init; }

    public int MaxConcurrentRuns { get; init; } = 10;
}
