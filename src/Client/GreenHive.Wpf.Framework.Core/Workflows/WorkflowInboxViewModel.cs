using System.Text.Json.Nodes;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>The inbox: tasks of the signed-in user and AI proposals to approve; the selected task is shown by the step presenter.</summary>
public sealed class WorkflowInboxViewModel : ResultViewModel
{
    private InboxItem? _selected;
    private InstanceDetails? _details;
    private IReadOnlyList<WorkflowSummary> _startable = [];

    public event EventHandler? ItemsChanged;

    public WorkflowManager Workflows => Context<IWorkflowContext>().Workflows;

    public IReadOnlyList<InboxItem> Items => Workflows.Inbox;

    public InboxItem? Selected
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    /// <summary>Instance and definition of the selected task (for command steps and the history).</summary>
    public InstanceDetails? Details
    {
        get => _details;
        private set => SetProperty(ref _details, value);
    }

    public IReadOnlyList<WorkflowSummary> Startable
    {
        get => _startable;
        private set => SetProperty(ref _startable, value);
    }

    public WorkflowStep? SelectedStep => Details?.Definition.FindStep(Selected?.StepId ?? string.Empty);

    protected override void OnCreated()
    {
        Title = T("workflow:view.inbox.label");
        Workflows.InboxChanged += (_, _) => ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _ = RefreshAsync();
        if (parameter is string id && Guid.TryParse(id, out var instanceId) && Items.FirstOrDefault(i => i.InstanceId == instanceId) is { } item)
        {
            _ = SelectAsync(item);
        }
    }

    public async Task RefreshAsync()
    {
        await Workflows.RefreshInboxAsync();
        await RunAsync(ct => Workflows.ListAsync(ct), list => Startable = list.Where(w => w.Status == WorkflowStatus.Active).ToList());
    }

    public async Task SelectAsync(InboxItem? item)
    {
        Selected = item;
        Details = null;
        ClearMessages();
        if (item is not null)
        {
            await RunAsync(ct => Workflows.InstanceAsync(item.InstanceId, ct), details => Details = details);
        }
    }

    public Task<bool> CompleteAsync(CompleteStepRequest request) =>
        Selected is not { } item
            ? Task.FromResult(false)
            : RunAsync(ct => Workflows.CompleteAsync(item.InstanceId, request, ct), _ => Selected = null, T("workflow:inbox.completed"));

    public Task<bool> HandToAiAsync() =>
        Selected is not { } item
            ? Task.FromResult(false)
            : RunAsync(ct => Workflows.HandOverAsync(item.InstanceId, WorkflowExecutor.Ai, null, ct), _ => Selected = null);

    public Task<bool> CancelSelectedAsync() =>
        Selected is not { } item ? Task.FromResult(false) : RunAsync(ct => Workflows.CancelAsync(item.InstanceId, ct), _ => Selected = null);

    public Task<bool> RunClientStepAsync() =>
        Selected is not { } item || SelectedStep is not { } step
            ? Task.FromResult(false)
            : RunAsync(ct => AppContext.Produce<WorkflowRunner>().RunAsync(item, step, ct), _ => Selected = null, T("workflow:inbox.completed"));

    public bool CanRunClientStep => Selected is { } item && AppContext.Produce<WorkflowRunner>().CanRun(item, SelectedStep);

    public Task<bool> StartAsync(WorkflowSummary workflow, WorkflowExecutor executor) =>
        RunAsync(ct => Workflows.StartAsync(workflow.Id, executor, null, ct), _ => { }, T("workflow:list.started"));

    public static string Describe(JsonObject variables) =>
        string.Join("   ", variables.Where(v => v.Value is JsonValue).Take(6).Select(v => $"{v.Key}: {v.Value}"));
}
