namespace GreenHive.NET.WorkflowContracts;

public enum AiAutonomy
{
    /// <summary>AI never runs this workflow.</summary>
    Off,

    /// <summary>AI pre-fills inputs; a human executes every step.</summary>
    Suggest,

    /// <summary>AI runs, but Critical steps wait for human approval.</summary>
    ApproveCritical,

    /// <summary>AI proposes each step; a human approves it.</summary>
    ApproveEachStep,

    /// <summary>AI runs all AiAllowed steps; Approval steps still need a human.</summary>
    Autonomous,
}

public enum AiFallback
{
    HandOverToHuman,
    Cancel,
}

/// <summary>Controls whether and how the AI agent may execute a workflow.</summary>
public sealed record AiExecutionPolicy
{
    public AiAutonomy Autonomy { get; init; } = AiAutonomy.Off;

    /// <summary>Allow list of step IDs (empty = all AiAllowed steps).</summary>
    public IReadOnlyList<string> AllowedSteps { get; init; } = [];

    /// <summary>Approvers (permission names) and approval timeout.</summary>
    public IReadOnlyList<string> Approvers { get; init; } = [];

    public int ApprovalTimeoutMinutes { get; init; } = 24 * 60;

    public int MaxRunsPerDay { get; init; } = 100;

    public int MaxStepsPerRun { get; init; } = 25;

    public int MaxTokensPerRun { get; init; } = 20_000;

    public int TimeLimitMinutes { get; init; } = 30;

    /// <summary>Variables that may be sent to the AI model (empty = all non-sensitive variables).</summary>
    public IReadOnlyList<string> DataScope { get; init; } = [];

    public string ModelProfile { get; init; } = "default";

    /// <summary>Required confidence (0..1) for automatic choices; below it the fallback applies.</summary>
    public double RequiredConfidence { get; init; } = 0.7;

    public AiFallback Fallback { get; init; } = AiFallback.HandOverToHuman;

    public bool AllowsStep(WorkflowStep step) =>
        Autonomy != AiAutonomy.Off && step.AiAllowed && !step.IsHumanOnly &&
        (AllowedSteps.Count == 0 || AllowedSteps.Contains(step.Id, StringComparer.OrdinalIgnoreCase));

    public bool NeedsApproval(WorkflowStep step) =>
        Autonomy == AiAutonomy.ApproveEachStep || (Autonomy == AiAutonomy.ApproveCritical && step.Critical);
}
