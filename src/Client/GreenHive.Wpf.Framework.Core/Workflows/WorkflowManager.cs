using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.ModuleLoader;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.NotificationClient;
using GreenHive.Wpf.Framework.Security;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>
/// The client side of workflows: API access (Result based), the inbox of the signed-in user (refreshed by workflow events),
/// module step types and templates (IWorkflowRegistrar), and reporting of workflow-capable commands for command triggers.
/// </summary>
public sealed class WorkflowManager : AppObject, IWorkflowRegistrar
{
    private readonly Dictionary<string, (string ModuleId, IWorkflowStepHandler Handler)> _stepTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<WorkflowDefinition>> _templates = new(StringComparer.OrdinalIgnoreCase);
    private WorkflowRecorder? _recorder;
    private Timer? _timer;
    private bool _refreshing;

    public event EventHandler? InboxChanged;

    public ObservableCollection<InboxItem> Inbox { get; } = [];

    public WorkflowRecorder Recorder => _recorder ??= AppContext.Produce<WorkflowRecorder>();

    public IReadOnlyDictionary<string, (string ModuleId, IWorkflowStepHandler Handler)> StepTypes => _stepTypes;

    public IReadOnlyList<WorkflowDefinition> Templates => _templates.Values.SelectMany(t => t).ToList();

    private BackendConnection Backend => Context<ISecurityContext>().Backend;

    // ---------------------------------------------------------------- lifecycle

    protected override void OnCreated()
    {
        var notifications = Context<INotificationContext>();
        notifications.Router.Subscribe("workflow.*", e => _ = RefreshInboxAsync());
        Backend.SignedIn += (_, _) =>
        {
            _ = RefreshInboxAsync();
            _timer = new Timer(state => AppContext.Dispatcher?.Run(() => _ = RefreshInboxAsync()), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        };
        Backend.SignedOut += (_, _) =>
        {
            _timer?.Dispose();
            Inbox.Clear();
            InboxChanged?.Invoke(this, EventArgs.Empty);
        };
        Context<ICommandContext>().Commands.Executor.Executed += OnCommandExecuted;
    }

    public async Task RefreshInboxAsync()
    {
        if (!Backend.IsSignedIn || _refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var items = await Backend.GetAsync<IReadOnlyList<InboxItem>>("/api/workflows/inbox");
            if (items.TryGetValue(out var list))
            {
                AppContext.Dispatcher?.Run(() =>
                {
                    Inbox.Clear();
                    foreach (var item in list.OrderByDescending(i => i.Since))
                    {
                        Inbox.Add(item);
                    }

                    InboxChanged?.Invoke(this, EventArgs.Empty);
                });
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ---------------------------------------------------------------- API

    public Task<Result<IReadOnlyList<WorkflowSummary>>> ListAsync(CancellationToken ct = default) =>
        Backend.GetAsync<IReadOnlyList<WorkflowSummary>>("/api/workflows", ct);

    public Task<Result<WorkflowDefinition>> GetAsync(Guid id, CancellationToken ct = default) =>
        Backend.GetAsync<WorkflowDefinition>($"/api/workflows/{id}", ct);

    public Task<Result<IReadOnlyList<WorkflowDefinition>>> VersionsAsync(Guid id, CancellationToken ct = default) =>
        Backend.GetAsync<IReadOnlyList<WorkflowDefinition>>($"/api/workflows/{id}/versions", ct);

    public Task<Result<SaveWorkflowResponse>> SaveAsync(WorkflowDefinition definition, bool publish, CancellationToken ct = default) =>
        Backend.PostAsync<SaveWorkflowResponse>("/api/workflows", new SaveWorkflowRequest(definition, publish), ct);

    public Task<Result<WorkflowDefinition>> SetStatusAsync(Guid id, WorkflowStatus status, CancellationToken ct = default) =>
        Backend.PostAsync<WorkflowDefinition>($"/api/workflows/{id}/status/{status}", null, ct);

    public Task<Result<WorkflowInstance>> StartAsync(Guid id, WorkflowExecutor executor, JsonObject? variables = null, CancellationToken ct = default) =>
        Backend.PostAsync<WorkflowInstance>($"/api/workflows/{id}/start", new StartWorkflowRequest(variables, executor), ct)
            .TapAsync(_ => RefreshInboxAsync());

    public Task<Result<WorkflowInstance>> CompleteAsync(Guid instanceId, CompleteStepRequest request, CancellationToken ct = default) =>
        Backend.PostAsync<WorkflowInstance>($"/api/workflows/instances/{instanceId}/complete", request, ct)
            .TapAsync(_ => RefreshInboxAsync());

    public Task<Result<WorkflowInstance>> HandOverAsync(Guid instanceId, WorkflowExecutor executor, string? reason = null, CancellationToken ct = default) =>
        Backend.PostAsync<WorkflowInstance>($"/api/workflows/instances/{instanceId}/handover", new HandOverRequest(executor, reason), ct)
            .TapAsync(_ => RefreshInboxAsync());

    public Task<Result<WorkflowInstance>> CancelAsync(Guid instanceId, CancellationToken ct = default) =>
        Backend.PostAsync<WorkflowInstance>($"/api/workflows/instances/{instanceId}/cancel", null, ct).TapAsync(_ => RefreshInboxAsync());

    public Task<Result<WorkflowInstance>> ResumeAsync(Guid instanceId, CancellationToken ct = default) =>
        Backend.PostAsync<WorkflowInstance>($"/api/workflows/instances/{instanceId}/resume", null, ct).TapAsync(_ => RefreshInboxAsync());

    public Task<Result<InstanceDetails>> InstanceAsync(Guid instanceId, CancellationToken ct = default) =>
        Backend.GetAsync<InstanceDetails>($"/api/workflows/instances/{instanceId}", ct);

    public Task<Result<IReadOnlyList<InstanceSummary>>> InstancesAsync(Guid? workflowId = null, bool mine = false, CancellationToken ct = default) =>
        Backend.GetAsync<IReadOnlyList<InstanceSummary>>($"/api/workflows/instances?mine={mine.ToString().ToLowerInvariant()}" +
            (workflowId is { } id ? $"&workflowId={id}" : string.Empty), ct);

    public Task<Result<IReadOnlyList<WorkflowImpactReport>>> ImpactReportsAsync(CancellationToken ct = default) =>
        Backend.GetAsync<IReadOnlyList<WorkflowImpactReport>>("/api/workflows/impact", ct);

    public Task<Result<AiStatus>> AiStatusAsync(CancellationToken ct = default) => Backend.GetAsync<AiStatus>("/api/ai/status", ct);

    public Task<Result<IReadOnlyList<AiAuditEntry>>> AiAuditAsync(Guid? instanceId = null, CancellationToken ct = default) =>
        Backend.GetAsync<IReadOnlyList<AiAuditEntry>>("/api/ai/audit" + (instanceId is { } id ? $"?instanceId={id}" : string.Empty), ct);

    public Task<Result<AiSwitchRequest>> SetAiEnabledAsync(bool enabled, CancellationToken ct = default) =>
        Backend.PostAsync<AiSwitchRequest>("/api/ai/switch", new AiSwitchRequest(enabled), ct);

    // ---------------------------------------------------------------- IWorkflowRegistrar (modules)

    public void RegisterStepType(string moduleId, IWorkflowStepHandler handler) => _stepTypes[handler.Describe().Id] = (moduleId, handler);

    public void RegisterTemplate(string moduleId, WorkflowDefinition template)
    {
        if (!_templates.TryGetValue(moduleId, out var list))
        {
            _templates[moduleId] = list = [];
        }

        list.RemoveAll(t => t.Id == template.Id);
        list.Add(template);
    }

    public void UnregisterModule(string moduleId)
    {
        foreach (var key in _stepTypes.Where(kv => kv.Value.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList())
        {
            _stepTypes.Remove(key);
        }

        _templates.Remove(moduleId);
    }

    // ---------------------------------------------------------------- command triggers

    private void OnCommandExecuted(object? sender, CommandExecutedEventArgs e)
    {
        if (e.Error is not null || !Backend.IsSignedIn)
        {
            return;
        }

        var descriptor = Context<ICommandContext>().Commands.GetDescriptor(e.CommandId);
        if (descriptor?.WorkflowCapable != true || e.CommandId.StartsWith("navigate.", StringComparison.Ordinal) || e.CommandId.StartsWith("shell.", StringComparison.Ordinal))
        {
            return;
        }

        Recorder.Record(e.CommandId, e.Parameter);
        var parameters = e.Parameter switch
        {
            null => new JsonObject(),
            string s => new JsonObject { ["parameter"] = s },
            _ => JsonSerializer.SerializeToNode(e.Parameter) as JsonObject ?? new JsonObject { ["parameter"] = e.Parameter.ToString() },
        };
        _ = Backend.PostAsync<JsonElement>($"/api/workflows/triggers/command/{e.CommandId}", parameters);
    }
}
