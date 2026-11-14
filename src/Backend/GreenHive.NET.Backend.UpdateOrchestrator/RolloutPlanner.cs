using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.UpdateOrchestrator;

/// <summary>Pure rules of a rollout (no I/O).</summary>
public static class RolloutPlanner
{
    /// <summary>First stage of a new rollout: held when workflows would break, otherwise pilot.</summary>
    public static (RolloutStage Stage, string? HoldReason) Start(int brokenWorkflows) =>
        brokenWorkflows > 0
            ? (RolloutStage.Held, $"{brokenWorkflows} workflow(s) would break with this version. Review them in the impact overview, then resume the rollout.")
            : (RolloutStage.Pilot, null);

    public static Result<RolloutStage> Advance(RolloutInfo rollout, bool force) => rollout.Stage switch
    {
        RolloutStage.Held when rollout.BrokenWorkflows > 0 && !force =>
            Error.Conflict("updates.rollout.held", "updates:rollout.heldBrokenWorkflows").WithMetadata("broken", rollout.BrokenWorkflows),
        RolloutStage.Held => RolloutStage.Pilot,
        RolloutStage.Pilot => RolloutStage.Broad,
        RolloutStage.Broad => RolloutStage.Complete,
        _ => Error.Conflict("updates.rollout.finished", "updates:rollout.finished"),
    };

    public static Result<RolloutStage> Hold(RolloutInfo rollout) =>
        rollout.Stage is RolloutStage.Pilot or RolloutStage.Broad ? RolloutStage.Held : Error.Conflict("updates.rollout.notActive", "updates:rollout.notActive");

    public static Result<RolloutStage> Cancel(RolloutInfo rollout) =>
        rollout.Stage is RolloutStage.Complete or RolloutStage.Cancelled ? Error.Conflict("updates.rollout.finished", "updates:rollout.finished") : RolloutStage.Cancelled;

    /// <summary>The version a user should run while this rollout is the latest one of its module.</summary>
    public static string VersionFor(RolloutInfo rollout, Guid userId) => rollout.Stage switch
    {
        RolloutStage.Pilot when rollout.PilotUsers.Contains(userId) => rollout.ToVersion,
        RolloutStage.Broad or RolloutStage.Complete => rollout.ToVersion,
        _ => rollout.FromVersion ?? rollout.ToVersion,
    };
}
