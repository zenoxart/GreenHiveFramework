using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Security;
using GreenHive.NET.Contracts;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>
/// Evaluates workflow triggers on the server: events (from the NotificationPoint), commands (reported by clients),
/// schedules ("every:15" minutes or "daily:07:00" UTC) and conditions (polled module APIs, edge-triggered).
/// De-duplication and maximum concurrent runs per trigger; suspended triggers are skipped.
/// </summary>
public sealed class TriggerEngine(
    WorkflowRepository repository,
    WorkflowEngine engine,
    ServiceClient services,
    TokenBroker tokens,
    TimeProvider time,
    ILogger<TriggerEngine> logger)
{
    private readonly Dictionary<string, bool> _conditionState = [];
    private readonly Dictionary<string, DateTimeOffset> _conditionPolled = [];
    private IReadOnlyList<WorkflowDefinition> _active = [];
    private DateTimeOffset _activeLoaded = DateTimeOffset.MinValue;

    public void Invalidate() => _activeLoaded = DateTimeOffset.MinValue;

    public async Task OnEventsAsync(IReadOnlyList<NotificationEnvelope> envelopes, CancellationToken ct)
    {
        foreach (var envelope in envelopes)
        {
            await engine.ResumeOnEventAsync(envelope, ct);
            var payload = envelope.Payload?.DeepClone().AsObject() ?? [];
            foreach (var definition in await ActiveAsync(envelope.TenantId, ct))
            {
                foreach (var trigger in definition.Triggers.Where(t => t.Kind == WorkflowTriggerKind.Event &&
                             string.Equals(t.EventType, envelope.Type, StringComparison.OrdinalIgnoreCase)))
                {
                    await FireAsync(definition, trigger, payload, envelope.Id.ToString(), $"event:{envelope.Type}", null, envelope.TenantId, ct);
                }
            }
        }
    }

    /// <summary>A client reports that a command ran; command triggers start their workflows (RunAs = the user, unless configured).</summary>
    public async Task<Result<int>> OnCommandAsync(string commandId, JsonObject? parameters, GreenHiveUser user, CancellationToken ct)
    {
        var started = 0;
        foreach (var definition in await ActiveAsync(user.TenantId, ct))
        {
            foreach (var trigger in definition.Triggers.Where(t => t.Kind == WorkflowTriggerKind.Command &&
                         string.Equals(t.CommandId, commandId, StringComparison.OrdinalIgnoreCase)))
            {
                if (await FireAsync(definition, trigger, parameters ?? [], Guid.NewGuid().ToString("n"), $"command:{commandId}", user, user.TenantId, ct))
                {
                    started++;
                }
            }
        }

        return started;
    }

    /// <summary>Schedules and conditions (called every 30 seconds by the scheduler).</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var definition in await ActiveAsync(TenantId.Demo.Value, ct))
        {
            foreach (var trigger in definition.Triggers)
            {
                if (trigger.Kind == WorkflowTriggerKind.Schedule && trigger.Schedule is { } schedule)
                {
                    var last = (await repository.LastScheduleRunAsync(definition.Id, trigger.Id, ct)).GetValueOrDefault(Option<DateTimeOffset>.None);
                    if (IsDue(schedule, last, now))
                    {
                        await repository.SetScheduleRunAsync(definition.Id, trigger.Id, now, ct);
                        await FireAsync(definition, trigger, new JsonObject { ["scheduledAt"] = now.ToString("O") },
                            $"{now:yyyyMMddHHmm}", $"schedule:{schedule}", null, TenantId.Demo.Value, ct);
                    }
                }
                else if (trigger.Kind == WorkflowTriggerKind.Condition && trigger.ConditionUrl is { } url && trigger.RunAs is { } runAs)
                {
                    var key = $"{definition.Id}:{trigger.Id}";
                    if (_conditionPolled.TryGetValue(key, out var polled) && now - polled < TimeSpan.FromSeconds(Math.Max(30, trigger.IntervalSeconds)))
                    {
                        continue;
                    }

                    _conditionPolled[key] = now;
                    var value = await tokens.ForAsync(runAs, "workflow-engine", ct)
                        .BindAsync(token => services.GetAsync<JsonElement>("api-gateway", url, token.AccessToken, ct));
                    var isTrue = value.Match(
                        v => v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("value", out var p) && p.ValueKind == JsonValueKind.True),
                        _ => false);
                    var wasTrue = _conditionState.GetValueOrDefault(key);
                    _conditionState[key] = isTrue;
                    if (isTrue && !wasTrue)
                    {
                        await FireAsync(definition, trigger, new JsonObject { ["condition"] = url }, $"{now:yyyyMMddHHmm}", $"condition:{url}", null, TenantId.Demo.Value, ct);
                    }
                }
            }
        }
    }

    private async Task<bool> FireAsync(WorkflowDefinition definition, WorkflowTrigger trigger, JsonObject payload, string fallbackKey, string triggeredBy,
        GreenHiveUser? caller, Guid tenantId, CancellationToken ct)
    {
        if (!trigger.Enabled)
        {
            return false;
        }

        if (trigger.Suspended)
        {
            logger.LogInformation("Trigger {Trigger} of '{Workflow}' is suspended ({Reason}) - skipped", trigger.Id, definition.Name, trigger.SuspendedReason);
            return false;
        }

        var scope = WorkflowExpression.Scope(null, null, null, payload);
        if (!WorkflowExpression.EvaluateCondition(trigger.Filter, scope).GetValueOrDefault(false))
        {
            return false;
        }

        var runAs = trigger.RunAs ?? caller?.Id ?? definition.OwnerId;
        if (runAs is not { } runAsId)
        {
            logger.LogWarning("Trigger {Trigger} of '{Workflow}' has no RunAs user - skipped", trigger.Id, definition.Name);
            return false;
        }

        if ((await repository.CountRunningAsync(definition.Id, ct)).GetValueOrDefault(0) >= trigger.MaxConcurrentRuns)
        {
            logger.LogWarning("Trigger {Trigger} of '{Workflow}' reached its maximum of concurrent runs", trigger.Id, definition.Name);
            return false;
        }

        var dedupKey = trigger.DeduplicationKey is { } dedupExpression
            ? Convert.ToString(WorkflowExpression.Evaluate(dedupExpression, scope).GetValueOrDefault(fallbackKey), CultureInfo.InvariantCulture) ?? fallbackKey
            : fallbackKey;
        var variables = new JsonObject();
        foreach (var (variable, expression) in trigger.PayloadMapping)
        {
            variables[variable] = WorkflowExpression.ToNode(WorkflowExpression.Evaluate(expression, scope).GetValueOrDefault(null));
        }

        var runAsName = caller?.DisplayName ?? (await tokens.ForAsync(runAsId, "workflow-engine", ct)).Match(t => t.User.DisplayName, _ => runAsId.ToString());
        var instanceId = Guid.NewGuid();
        var recorded = await repository.TryRecordTriggerRunAsync(definition.Id, trigger.Id, dedupKey, instanceId, ct);
        if (!recorded.GetValueOrDefault(false))
        {
            return false;
        }

        var started = await engine.StartAsync(definition, new StartContext(tenantId, runAsId, runAsName, trigger.Executor, triggeredBy)
        {
            Variables = variables,
            Payload = payload,
        }, ct);
        started.TapError(error => logger.LogWarning("Trigger {Trigger} of '{Workflow}' could not start: {Error}", trigger.Id, definition.Name, error));
        return started.IsSuccess;
    }

    private async Task<IReadOnlyList<WorkflowDefinition>> ActiveAsync(Guid tenantId, CancellationToken ct)
    {
        if (time.GetUtcNow() - _activeLoaded > TimeSpan.FromSeconds(15))
        {
            _active = (await repository.LatestAsync(tenantId, ct)).GetValueOrDefault([]).Where(d => d.Status == WorkflowStatus.Active).ToList();
            _activeLoaded = time.GetUtcNow();
        }

        return _active;
    }

    private static bool IsDue(string schedule, Option<DateTimeOffset> last, DateTimeOffset now)
    {
        var parts = schedule.Split(':', 2);
        if (parts[0].Equals("every", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts.ElementAtOrDefault(1), out var minutes) && minutes > 0)
        {
            return last.Match(l => now - l >= TimeSpan.FromMinutes(minutes), () => true);
        }

        if (parts[0].Equals("daily", StringComparison.OrdinalIgnoreCase) && TimeOnly.TryParse(parts.ElementAtOrDefault(1), CultureInfo.InvariantCulture, out var at))
        {
            var todayAt = new DateTimeOffset(now.UtcDateTime.Date + at.ToTimeSpan(), TimeSpan.Zero);
            return now >= todayAt && last.Match(l => l < todayAt, () => true);
        }

        return false;
    }
}

/// <summary>Runs schedules, conditions, retries and wait timeouts every 30 seconds.</summary>
public sealed class WorkflowScheduler(IServiceProvider services, ILogger<WorkflowScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await services.GetRequiredService<TriggerEngine>().TickAsync(stoppingToken);
                await services.GetRequiredService<WorkflowEngine>().MaintenanceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Workflow scheduler tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
