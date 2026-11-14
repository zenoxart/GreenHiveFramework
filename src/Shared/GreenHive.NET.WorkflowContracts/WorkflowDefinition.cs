namespace GreenHive.NET.WorkflowContracts;

public enum WorkflowOrigin
{
    ModuleTemplate,
    Tenant,
    User,
}

public enum WorkflowStatus
{
    Draft,
    Active,
    Disabled,
    NeedsReview,
    Archived,
}

/// <summary>A declared workflow variable.</summary>
public sealed record WorkflowVariable(string Name, string Type = "text")
{
    /// <summary>Sensitive values are masked in logs and never sent to the AI model.</summary>
    public bool Sensitive { get; init; }
}

/// <summary>Who may view, edit and start a workflow (permission names; empty = everyone in the tenant).</summary>
public sealed record WorkflowPermissions
{
    public IReadOnlyList<string> View { get; init; } = [];

    public IReadOnlyList<string> Edit { get; init; } = [];

    public IReadOnlyList<string> Start { get; init; } = [];
}

/// <summary>
/// A versioned, declarative description of a workflow. Data only - never code - so it can be stored, versioned,
/// analysed, migrated and executed by a human or by AI. Published versions are immutable; running instances keep
/// the version they started with.
/// </summary>
public sealed record WorkflowDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int Version { get; init; } = 1;

    public required string Name { get; init; }

    public string? Description { get; init; }

    public WorkflowOrigin Origin { get; init; } = WorkflowOrigin.Tenant;

    public string? OwningModule { get; init; }

    public required string StartStepId { get; init; }

    public IReadOnlyList<WorkflowStep> Steps { get; init; } = [];

    public IReadOnlyList<WorkflowTransition> Transitions { get; init; } = [];

    public IReadOnlyList<WorkflowVariable> Variables { get; init; } = [];

    public IReadOnlyList<WorkflowTrigger> Triggers { get; init; } = [];

    public AiExecutionPolicy AiPolicy { get; init; } = new();

    public IReadOnlyList<WorkflowDependency> Dependencies { get; init; } = [];

    public WorkflowStatus Status { get; init; } = WorkflowStatus.Draft;

    public WorkflowPermissions Permissions { get; init; } = new();

    public Guid? OwnerId { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public WorkflowStep? FindStep(string stepId) => Steps.FirstOrDefault(s => s.Id.Equals(stepId, StringComparison.OrdinalIgnoreCase));

    public bool IsSensitive(string variable) =>
        Variables.Any(v => v.Sensitive && v.Name.Equals(variable, StringComparison.OrdinalIgnoreCase));
}
