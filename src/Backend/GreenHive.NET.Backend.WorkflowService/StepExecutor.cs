using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>
/// Executes headless steps (module step types and workflow-capable commands) through the API gateway, on behalf of the
/// RunAs user (delegated token, actor = workflow-engine or ai-agent) - so the same module use case, the same
/// permissions and the same errors apply as for a person in the app.
/// </summary>
public sealed class StepExecutor(ModuleDirectory modules, TokenBroker tokens, ServiceClient services)
{
    public Task<Result<JsonObject>> ExecuteAsync(WorkflowStep step, JsonObject inputs, Guid runAs, string actor, CancellationToken ct) =>
        modules.EndpointAsync(step, ct)
            .BindAsync(endpoint => tokens.ForAsync(runAs, actor, ct).Map(token => (Endpoint: endpoint, Token: token.AccessToken)))
            .BindAsync(x => services.PostAsync<JsonObject>("api-gateway", x.Endpoint, inputs, x.Token, ct))
            .Map(outputs => outputs ?? new JsonObject());
}

/// <summary>Hands interactive and AI steps to the AiAgentService (POST /internal/runs, processed asynchronously).</summary>
public sealed class AiDispatcher(ServiceClient services, ILogger<AiDispatcher> logger)
{
    public async Task<Result<Unit>> RequestAsync(Guid instanceId, string stepId, CancellationToken ct)
    {
        var sent = await services.PostAsync<System.Text.Json.JsonElement?>("ai-agent-service", "/internal/runs", new AiRunRequest(instanceId, stepId), null, ct);
        if (sent.IsFailure)
        {
            logger.LogWarning("AI run for instance {Instance} step {Step} could not be requested: {Error}", instanceId, stepId, sent.Error);
        }

        return sent.ToUnit();
    }
}

/// <summary>Published definition versions are immutable, so they can be cached by (id, version).</summary>
public sealed class DefinitionCache(WorkflowRepository repository)
{
    private readonly ConcurrentDictionary<(Guid, int), WorkflowDefinition> _cache = new();

    public async Task<Result<WorkflowDefinition>> GetAsync(Guid id, int version, CancellationToken ct)
    {
        if (_cache.TryGetValue((id, version), out var cached))
        {
            return cached;
        }

        var loaded = await repository.FindVersionAsync(id, version, ct);
        return loaded.Tap(d => _cache[(id, version)] = d);
    }

    public void Forget(Guid id, int version) => _cache.TryRemove((id, version), out _);
}
