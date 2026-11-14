// WorkflowService (Docker container "workflow-service", route /api/workflows)
// Stores versioned workflow definitions and instances, runs workflows server-side (headless steps via the module
// backends on behalf of the RunAs user), hands interactive steps to the user's inbox and AI steps to the AiAgentService,
// evaluates triggers (events, commands, schedules, conditions) and analyses the impact of module updates.
