// AiAgentService (Docker container "ai-agent-service", route /api/ai)
// Executes workflow steps with an AI model when an AI-executed trigger fires or a user hands an instance over to the AI.
// Every proposal passes the guardrails (kill switch, policy, limits, data scope, schema validation, loop detection,
// confidence); critical steps and ApproveEachStep policies create approval requests; everything is audited.
// Providers: "rules" (deterministic, offline - default for the demo) and "openai" (any OpenAI-compatible endpoint).
