// UpdateOrchestrator (Docker container "update-orchestrator", route /api/updates)
// Staged rollouts of module versions: Pilot (pilot users from the setting updates.pilotUsers) -> Broad -> Complete.
// A rollout is held while the impact analysis reports broken workflows (advancing needs force).
// Clients ask for their assignments and hot-swap modules when a new version is assigned to them.
