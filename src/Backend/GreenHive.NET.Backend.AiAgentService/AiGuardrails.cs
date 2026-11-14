using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Backend.AiAgentService;

/// <summary>
/// Enforces the AiExecutionPolicy before anything the AI proposes is executed. Each guardrail is a pipeline step returning
/// Result: a violated rule yields Forbidden (or Validation for out-of-schema values) with a code recorded in the audit log.
/// </summary>
public sealed class AiGuardrails(Db db, ServiceClient services, AiAuditLog audit)
{
    /// <summary>Checks before the model is asked: kill switches, policy, limits and loops.</summary>
    public async Task<Result<Unit>> BeforeAsync(WorkflowInstance instance, WorkflowDefinition definition, WorkflowStep step, Guid tenantId, CancellationToken ct)
    {
        var global = await db.ScalarAsync<bool?>("select enabled from ai.switches where scope = 'global'", null, ct);
        if (global.GetValueOrDefault(true) == false)
        {
            return Error.Forbidden("ai.killSwitch.global", "ai:disabled");
        }

        var tenant = await services.GetAsync<ConfigSetting>("configuration-service", $"/internal/settings/{SettingKeys.AiEnabled}?tenant={tenantId}", null, ct);
        if (tenant.TryGetValue(out var setting) && setting.Value.ValueKind == JsonValueKind.False)
        {
            return Error.Forbidden("ai.killSwitch.tenant", "ai:disabled");
        }

        var policy = definition.AiPolicy;
        if (policy.Autonomy == AiAutonomy.Off)
        {
            return Error.Forbidden("ai.policy.off", "ai:policyOff");
        }

        if (!policy.AllowsStep(step) && step.Kind != WorkflowStepKind.AiTask)
        {
            return Error.Forbidden("ai.policy.step", "ai:stepNotAllowed");
        }

        var usage = await audit.UsageAsync(definition.Id, instance.Id, step.Id, ct);
        if (!usage.TryGetValue(out var u, out var error))
        {
            return error;
        }

        return u switch
        {
            _ when u.RunsToday > policy.MaxRunsPerDay => Error.Forbidden("ai.limit.runsPerDay", "ai:limitRunsPerDay"),
            _ when u.InstanceEntries >= policy.MaxStepsPerRun => Error.Forbidden("ai.limit.stepsPerRun", "ai:limitSteps"),
            _ when u.InstanceTokens >= policy.MaxTokensPerRun => Error.Forbidden("ai.limit.tokens", "ai:limitTokens"),
            _ when u.StepEntries >= 3 => Error.Forbidden("ai.loop", "ai:loopDetected"),
            _ when DateTimeOffset.UtcNow - instance.CreatedAt > TimeSpan.FromMinutes(policy.TimeLimitMinutes) && instance.Executor == WorkflowExecutor.Ai &&
                   instance.History.All(h => h.ActorKind != ActorKind.Human) => Error.Forbidden("ai.limit.time", "ai:limitTime"),
            _ => Result.Success(),
        };
    }

    /// <summary>The data the model may see: non-sensitive variables inside the data scope, the payload and earlier step outputs.</summary>
    public static JsonObject Context(WorkflowInstance instance, WorkflowDefinition definition)
    {
        var scope = definition.AiPolicy.DataScope;
        bool Allowed(string name) => !definition.IsSensitive(name) && (scope.Count == 0 || scope.Contains(name, StringComparer.OrdinalIgnoreCase));
        var variables = new JsonObject();
        foreach (var (key, value) in instance.Variables)
        {
            if (Allowed(key))
            {
                variables[key] = value?.DeepClone();
            }
        }

        var sensitiveFields = definition.Steps.SelectMany(s => s.Input?.Fields ?? []).Where(f => f.Sensitive).Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var steps = new JsonObject();
        foreach (var (stepId, outputs) in instance.StepOutputs)
        {
            steps[stepId] = outputs is JsonObject o ? InputValidator.Mask(o, n => sensitiveFields.Contains(n) || definition.IsSensitive(n)) : outputs?.DeepClone();
        }

        return new JsonObject
        {
            ["workflow"] = definition.Name,
            ["variables"] = variables,
            ["payload"] = instance.TriggerPayload?.DeepClone() ?? new JsonObject(),
            ["steps"] = steps,
        };
    }

    /// <summary>Checks the model's answer: schema, options, confidence.</summary>
    public static Result<(JsonObject Outputs, string? Decision)> After(WorkflowDefinition definition, WorkflowStep step, AiCompletion completion)
    {
        if (step.Kind == WorkflowStepKind.Choice)
        {
            return step.Options.Any(o => o.Value.Equals(completion.Decision, StringComparison.OrdinalIgnoreCase))
                ? (completion.Outputs, completion.Decision)
                : Error.Validation("ai.output.choice", "ai:invalidChoice");
        }

        var validated = step.Input is { } schema ? InputValidator.Validate(schema, completion.Outputs).ToResult() : completion.Outputs;
        return validated
            .Ensure(_ => completion.Confidence >= definition.AiPolicy.RequiredConfidence,
                Error.Forbidden("ai.confidence.low", "ai:lowConfidence").WithMetadata("confidence", completion.Confidence))
            .Map(outputs => (outputs, completion.Decision));
    }
}
