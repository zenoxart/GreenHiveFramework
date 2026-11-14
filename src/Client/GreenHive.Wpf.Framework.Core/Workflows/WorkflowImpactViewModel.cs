using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>Impact overview: one report per module update with the contract diff and the classification of every workflow.</summary>
public sealed class WorkflowImpactViewModel : ResultViewModel
{
    private IReadOnlyList<WorkflowImpactReport> _reports = [];
    private WorkflowImpactReport? _selected;

    public IReadOnlyList<WorkflowImpactReport> Reports
    {
        get => _reports;
        private set => SetProperty(ref _reports, value);
    }

    public WorkflowImpactReport? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    private WorkflowManager Workflows => Context<IWorkflowContext>().Workflows;

    protected override void OnCreated()
    {
        Title = T("workflow:view.impact.label");
        Context<GreenHive.Wpf.Framework.NotificationClient.INotificationContext>().Router
            .Subscribe(GreenHive.NET.NotificationContracts.EventTypes.WorkflowImpactDetected, e => _ = LoadAsync());
    }

    public override void OnNavigatedTo(object? parameter) =>
        _ = LoadAsync(parameter is string id && Guid.TryParse(id, out var reportId) ? reportId : null);

    public Task<bool> LoadAsync(Guid? select = null) =>
        RunAsync(ct => Workflows.ImpactReportsAsync(ct), list =>
        {
            Reports = list;
            Selected = list.FirstOrDefault(r => r.Id == (select ?? Selected?.Id)) ?? list.FirstOrDefault();
        });

    public void OpenWorkflow(Guid workflowId) =>
        Context<GreenHive.Wpf.Framework.Navigation.INavigationContext>().Navigation.NavigateTo(WorkflowViews.Workflows, workflowId.ToString());
}
