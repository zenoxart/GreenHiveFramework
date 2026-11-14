namespace GreenHive.NET.WorkflowContracts;

public enum DependencyKind
{
    Module,
    Command,
    InputSchema,
    StepType,
    Event,
    BackendApi,
}

/// <summary>One thing a workflow relies on; computed when the definition is saved; basis of the impact analysis.</summary>
public sealed record WorkflowDependency(DependencyKind Kind, string Id, string Version)
{
    public IReadOnlyList<string> UsedBySteps { get; init; } = [];
}
