using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Navigation;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>Registers the WPF views of the workflow area as navigation targets (called by the host).</summary>
public static class WorkflowViewRegistration
{
    public static void Register(NavigationRegistry registry)
    {
        registry.Register(new NavigationTarget(WorkflowViews.Inbox, "workflow")
        {
            TextKey = "workflow:view.inbox", Icon = "workflow.inbox", Group = "workflow", Order = 0, IsModuleHome = true, ShowInViewMenu = true,
            View = new ViewDescriptor { Role = ViewRole.Document },
            CreateViewModel = c => c.Produce<WorkflowInboxViewModel>(),
            CreateView = vm => new WorkflowInboxView((WorkflowInboxViewModel)vm),
        });
        registry.Register(new NavigationTarget(WorkflowViews.Workflows, "workflow")
        {
            TextKey = "workflow:view.workflows", Icon = "workflow", Group = "workflow", Order = 1, ShowInViewMenu = true, Permission = "workflows.view",
            View = new ViewDescriptor { Role = ViewRole.Document },
            CreateViewModel = c => c.Produce<WorkflowDesignerViewModel>(),
            CreateView = vm => new WorkflowDesignerView((WorkflowDesignerViewModel)vm),
        });
        registry.Register(new NavigationTarget(WorkflowViews.Impact, "workflow")
        {
            TextKey = "workflow:view.impact", Icon = "workflow.impact", Group = "workflow", Order = 2, ShowInViewMenu = true, Permission = "workflows.view",
            View = new ViewDescriptor { Role = ViewRole.Document },
            CreateViewModel = c => c.Produce<WorkflowImpactViewModel>(),
            CreateView = vm => new WorkflowImpactView((WorkflowImpactViewModel)vm),
        });
        registry.Register(new NavigationTarget(WorkflowViews.Ai, "workflow")
        {
            TextKey = "workflow:view.ai", Icon = "workflow.ai", Group = "workflow", Order = 3, ShowInViewMenu = true,
            View = new ViewDescriptor { Role = ViewRole.ToolWindow, DefaultDock = DockSide.Right, MinWidth = 280 },
            CreateViewModel = c => c.Produce<AiApprovalInboxViewModel>(),
            CreateView = vm => new AiApprovalInboxView((AiApprovalInboxViewModel)vm),
        });
    }
}
