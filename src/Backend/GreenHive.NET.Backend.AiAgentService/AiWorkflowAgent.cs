using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.NET.Backend.AiAgentService;

/// <summary>
/// Drives a workflow step with the AI model:
/// 1. load instance + definition, 2. guardrails (switches, policy, limits, loops), 3. build the scoped context,
/// 4. ask the model (with up to three tool calls), 5. validate (schema, confidence), 6. propose (approval needed) or
/// complete through the WorkflowEngine (same handlers and permissions as a person), 7. audit every decision.
/// On low confidence, validation failure or a policy limit the instance is handed over to a person instead of guessing.
/// </summary>
public sealed class AiWorkflowAgent(
    ServiceClient services,
    IAiProvider provider,
    AiGuardrails guardrails,
    AiToolCatalog tools,
    AiAuditLog audit,
    ILogger<AiWorkflowAgent> logger) : BackgroundService
{
    private readonly Channel<AiRunRequest> _queue = Channel.CreateUnbounded<AiRunRequest>();

    public void Enqueue(AiRunRequest request) => _queue.Writer.TryWrite(request);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunStepAsync(request, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AI run of {Instance} step {Step} failed", request.InstanceId, request.StepId);
            }
        }
    }

    public async Task RunStepAsync(AiRunRequest request, CancellationToken ct)
    {
        var details = await services.GetAsync<InstanceDetails>("workflow-service", $"/internal/instances/{request.InstanceId}", null, ct);
        if (!details.TryGetValue(out var d, out var loadError))
        {
            logger.LogWarning("AI run: instance {Instance} not loaded: {Error}", request.InstanceId, loadError);
            return;
        }

        var (instance, definition) = (d.Instance, d.Definition);
        if (!string.Equals(instance.CurrentStepId, request.StepId, StringComparison.OrdinalIgnoreCase) || definition.FindStep(request.StepId) is not { } step)
        {
            return; // the step moved on in the meantime
        }

        var tenantId = GreenHive.NET.Contracts.TenantId.Demo.Value;
        var policy = definition.AiPolicy;
        var context = AiGuardrails.Context(instance, definition);
        var promptText = context.ToJsonString();

        var allowed = await guardrails.BeforeAsync(instance, definition, step, tenantId, ct);
        if (allowed.IsFailure)
        {
            await audit.WriteAsync(tenantId, instance.Id, definition.Id, step.Id, provider, policy.ModelProfile, promptText, null, null, null, "blocked", allowed.Error.Code, 0, ct);
            await HandOverAsync(instance.Id, $"AI stopped: {allowed.Error.Code}", policy, ct);
            return;
        }

        var prompt = new AiPrompt(definition.Description ?? definition.Name, step, context, await tools.ToolsForAsync(step, ct));
        AiCompletion? completion = null;
        var tokens = 0;
        for (var round = 0; round < 4; round++)
        {
            var answer = await provider.CompleteAsync(prompt, ct);
            if (!answer.TryGetValue(out var a, out var providerError))
            {
                await audit.WriteAsync(tenantId, instance.Id, definition.Id, step.Id, provider, policy.ModelProfile, promptText, null, null, null, "error", providerError.Code, tokens, ct);
                await HandOverAsync(instance.Id, $"AI provider failed: {providerError.Code}", policy, ct);
                return;
            }

            tokens += a.Tokens;
            if (a.ToolCall is not { } call || round == 3)
            {
                completion = a;
                break;
            }

            // Tool call: only tools of the step, executed by the engine on behalf of the RunAs user.
            if (!prompt.Tools.Any(t => t.Id.Equals(call.ToolId, StringComparison.OrdinalIgnoreCase)))
            {
                await audit.WriteAsync(tenantId, instance.Id, definition.Id, step.Id, provider, policy.ModelProfile, promptText, call.Inputs, null, null, "blocked", "ai.tool.notAllowed", tokens, ct);
                await HandOverAsync(instance.Id, "AI tried to call a tool that is not allowed.", policy, ct);
                return;
            }

            var result = await services.PostAsync<JsonObject>("workflow-service", $"/internal/instances/{instance.Id}/tools/{call.ToolId}", call.Inputs, null, ct);
            await audit.WriteAsync(tenantId, instance.Id, definition.Id, step.Id, provider, policy.ModelProfile, promptText, call.Inputs,
                $"tool {call.ToolId}", null, "tool", result.IsFailure ? result.Error.Code : null, a.Tokens, ct);
            var toolResults = prompt.ToolResults.DeepClone().AsObject();
            toolResults[call.ToolId] = result.Match(r => (JsonNode)r.DeepClone(), e => new JsonObject { ["error"] = e.Code });
            prompt = prompt with { ToolResults = toolResults };
        }

        var checkedAnswer = AiGuardrails.After(definition, step, completion!);
        if (!checkedAnswer.TryGetValue(out var final, out var invalid))
        {
            await audit.WriteAsync(tenantId, instance.Id, definition.Id, step.Id, provider, policy.ModelProfile, promptText, completion!.Outputs,
                completion.Reasoning, completion.Confidence, "rejected", invalid.Code, tokens, ct);
            await HandOverAsync(instance.Id, $"AI result not accepted ({invalid.Code}): {completion.Reasoning}", policy, ct);
            return;
        }

        if (policy.NeedsApproval(step))
        {
            var proposed = await services.PostAsync<JsonElement>("workflow-service", $"/internal/instances/{instance.Id}/proposal",
                new AiProposalRequest(step.Id, final.Outputs, completion!.Reasoning, completion.Confidence) { Decision = final.Decision }, null, ct);
            await audit.WriteAsync(tenantId, instance.Id, definition.Id, step.Id, provider, policy.ModelProfile, promptText, final.Outputs,
                completion.Reasoning, completion.Confidence, "proposed", proposed.IsFailure ? proposed.Error.Code : null, tokens, ct);
            return;
        }

        var completed = await services.PostAsync<JsonElement>("workflow-service", $"/internal/instances/{instance.Id}/complete",
            new AiCompleteRequest(step.Id, final.Outputs, completion!.Reasoning, completion.Confidence) { Decision = final.Decision }, null, ct);
        await audit.WriteAsync(tenantId, instance.Id, definition.Id, step.Id, provider, policy.ModelProfile, promptText, final.Outputs,
            completion.Reasoning, completion.Confidence, completed.IsSuccess ? "executed" : "failed", completed.IsFailure ? completed.Error.Code : null, tokens, ct);
        if (completed.IsFailure)
        {
            await HandOverAsync(instance.Id, $"Completing the step failed: {completed.Error.Code}", policy, ct);
        }
        else
        {
            logger.LogInformation("AI completed step {Step} of {Instance} (confidence {Confidence:0.00})", step.Id, instance.Id, completion.Confidence);
        }
    }

    private Task<Result<JsonElement>> HandOverAsync(Guid instanceId, string reason, AiExecutionPolicy policy, CancellationToken ct) =>
        policy.Fallback == AiFallback.Cancel
            ? services.PostAsync<JsonElement>("workflow-service", $"/internal/instances/{instanceId}/handover", new HandOverRequest(WorkflowExecutor.Human, reason), null, ct)
            : services.PostAsync<JsonElement>("workflow-service", $"/internal/instances/{instanceId}/handover", new HandOverRequest(WorkflowExecutor.Mixed, reason), null, ct);
}
