using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>The AI agent panel: provider and usage, kill switch (ai.admin), proposals waiting for approval and the audit trail.</summary>
public sealed class AiApprovalInboxViewModel : ResultViewModel
{
    private AiStatus? _status;
    private IReadOnlyList<AiAuditEntry> _audit = [];

    public AiStatus? Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public IReadOnlyList<AiAuditEntry> Audit
    {
        get => _audit;
        private set => SetProperty(ref _audit, value);
    }

    public IReadOnlyList<InboxItem> Proposals => Workflows.Inbox.Where(i => i.Proposal is not null).ToList();

    public WorkflowManager Workflows => Context<IWorkflowContext>().Workflows;

    public bool CanSwitch => Has("ai.admin");

    protected override void OnCreated()
    {
        Title = T("workflow:view.ai.label");
        Workflows.InboxChanged += (_, _) => OnPropertyChanged(nameof(Proposals));
    }

    public override void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    public async Task LoadAsync()
    {
        await RunAsync(ct => Workflows.AiStatusAsync(ct), s => Status = s);
        if (Has("workflows.view"))
        {
            await RunAsync(ct => Workflows.AiAuditAsync(null, ct), a => Audit = a);
        }
    }

    public Task<bool> SetEnabledAsync(bool enabled) => RunAsync(ct => Workflows.SetAiEnabledAsync(enabled, ct), status => _ = LoadAsync());

    public Task<bool> DecideAsync(InboxItem item, bool approve) =>
        RunAsync(ct => Workflows.CompleteAsync(item.InstanceId, new CompleteStepRequest(item.StepId) { Decision = approve ? "approve" : "reject" }, ct));

    public void Open(InboxItem item) =>
        Context<GreenHive.Wpf.Framework.Navigation.INavigationContext>().Navigation.NavigateTo(WorkflowViews.Inbox, item.InstanceId.ToString());
}
