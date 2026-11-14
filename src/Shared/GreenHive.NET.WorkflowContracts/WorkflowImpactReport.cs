namespace GreenHive.NET.WorkflowContracts;

public enum SurfaceChangeKind
{
    Added,
    Removed,
    ChangedCompatible,
    ChangedBreaking,
    Renamed,
}

/// <summary>One difference between two contract surfaces of a module.</summary>
public sealed record SurfaceChange(DependencyKind Kind, string Id, SurfaceChangeKind Change, string Detail)
{
    /// <summary>For renames / replacements: the new ID.</summary>
    public string? ReplacedBy { get; init; }

    public bool IsBreaking => Change is SurfaceChangeKind.Removed or SurfaceChangeKind.ChangedBreaking;
}

public enum ImpactSeverity
{
    Unaffected,
    Compatible,
    AutoMigrated,
    ReviewRequired,
    Broken,
}

public sealed record ImpactReason(string StepId, DependencyKind Kind, string DependencyId, string Reason);

/// <summary>How one workflow is affected by a module update.</summary>
public sealed record WorkflowImpact
{
    public required Guid WorkflowId { get; init; }

    public required string Name { get; init; }

    public int Version { get; init; }

    public ImpactSeverity Severity { get; init; }

    public IReadOnlyList<ImpactReason> Reasons { get; init; } = [];

    public int RunningInstances { get; init; }

    /// <summary>What happened to running instances: continued, paused, migrated.</summary>
    public string InstanceAction { get; init; } = "continued";

    /// <summary>migrate, open in designer, disable, pin to old module version.</summary>
    public string SuggestedAction { get; init; } = "none";

    public bool AiTriggersSuspended { get; init; }

    public int? MigratedToVersion { get; init; }
}

/// <summary>Result of the impact analysis for one module update; shown in the client overview.</summary>
public sealed record WorkflowImpactReport
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string ModuleId { get; init; }

    public required string FromVersion { get; init; }

    public required string ToVersion { get; init; }

    public DateTimeOffset AnalyzedAt { get; init; }

    public string? RolloutStage { get; init; }

    public IReadOnlyList<SurfaceChange> SurfaceDiff { get; init; } = [];

    public IReadOnlyList<WorkflowImpact> Workflows { get; init; } = [];

    public int BrokenCount => Workflows.Count(w => w.Severity == ImpactSeverity.Broken);

    public int ReviewCount => Workflows.Count(w => w.Severity == ImpactSeverity.ReviewRequired);
}
