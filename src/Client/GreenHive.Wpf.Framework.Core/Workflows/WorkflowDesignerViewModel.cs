using System.Text.Json;
using GreenHive.NET.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>
/// Workflow list and details: steps, transitions, triggers, dependencies, AI policy, instances; start, activate,
/// edit as JSON (validated with the shared WorkflowAnalysis before saving) and record new workflows in the app.
/// </summary>
public sealed class WorkflowDesignerViewModel : ResultViewModel
{
    private IReadOnlyList<WorkflowSummary> _workflows = [];
    private WorkflowDefinition? _selected;
    private IReadOnlyList<InstanceSummary> _instances = [];

    public WorkflowManager Workflows => Context<IWorkflowContext>().Workflows;

    public IReadOnlyList<WorkflowSummary> All
    {
        get => _workflows;
        private set => SetProperty(ref _workflows, value);
    }

    public WorkflowDefinition? Selected
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    public IReadOnlyList<InstanceSummary> Instances
    {
        get => _instances;
        private set => SetProperty(ref _instances, value);
    }

    public bool CanEdit => Has("workflows.edit");

    protected override void OnCreated() => Title = T("workflow:view.workflows.label");

    public override void OnNavigatedTo(object? parameter)
    {
        _ = LoadAsync(parameter is string id && Guid.TryParse(id, out var workflowId) ? workflowId : null);
    }

    public async Task LoadAsync(Guid? select = null)
    {
        await RunAsync(ct => Workflows.ListAsync(ct), list => All = list);
        if ((select ?? Selected?.Id) is { } id)
        {
            await SelectAsync(id);
        }
    }

    public async Task SelectAsync(Guid? id)
    {
        if (id is not { } workflowId)
        {
            Selected = null;
            Instances = [];
            return;
        }

        await RunAsync(ct => Workflows.GetAsync(workflowId, ct), d => Selected = d);
        await RunAsync(ct => Workflows.InstancesAsync(workflowId, false, ct), list => Instances = list);
    }

    public Task<bool> StartAsync(WorkflowExecutor executor) =>
        Selected is not { } d ? Task.FromResult(false) : RunAsync(ct => Workflows.StartAsync(d.Id, executor, null, ct), started => _ = SelectAsync(d.Id), T("workflow:list.started"));

    public Task<bool> SetStatusAsync(WorkflowStatus status) =>
        Selected is not { } d ? Task.FromResult(false) : RunAsync(ct => Workflows.SetStatusAsync(d.Id, status, ct), updated => _ = LoadAsync(d.Id));

    public string ToJson(WorkflowDefinition definition) => JsonSerializer.Serialize(definition, new JsonSerializerOptions(GreenHiveJson.Options) { WriteIndented = true });

    /// <summary>Parses and validates JSON with the shared rules; returns the error text (null = valid).</summary>
    public Result<WorkflowDefinition> Parse(string json) =>
        Try.Run(() => JsonSerializer.Deserialize<WorkflowDefinition>(json, GreenHiveJson.Options)!, "workflow.json")
            .Bind(d => WorkflowAnalysis.Validate(d).ToResult());

    public Task<bool> SaveAsync(WorkflowDefinition definition, bool publish) =>
        RunAsync(ct => Workflows.SaveAsync(definition, publish, ct), r =>
        {
            InfoText = T("workflow:list.saved", ("version", r.Version), ("status", r.Status));
            _ = LoadAsync(r.Id);
        });

    public WorkflowDefinition NewDefinition() => new()
    {
        Name = "New workflow",
        StartStepId = "input",
        Steps = new WorkflowStep[]
        {
            new WorkflowStep
            {
                Id = "input",
                Kind = WorkflowStepKind.Input,
                Title = "Input",
                Input = new InputSchema("workflow.newInput") { Fields = new InputField[] { new InputField("note") { Label = "Note" } } },
            },
        },
        Transitions = new WorkflowTransition[] { new WorkflowTransition("input", WorkflowTransition.End) },
        Triggers = new WorkflowTrigger[] { new WorkflowTrigger { Kind = WorkflowTriggerKind.Manual } },
    };
}
