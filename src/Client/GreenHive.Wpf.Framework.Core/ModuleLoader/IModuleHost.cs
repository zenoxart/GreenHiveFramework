using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Docking;
using GreenHive.Wpf.Framework.Navigation;
using GreenHive.Wpf.Framework.NotificationClient;
using GreenHive.Wpf.Framework.Security;
using GreenHive.NET.WorkflowContracts;
using GreenHive.Wpf.Framework.XamlMetadata;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>Receives the workflow registrations of modules (implemented by the WorkflowManager).</summary>
public interface IWorkflowRegistrar
{
    void RegisterStepType(string moduleId, IWorkflowStepHandler handler);

    void RegisterTemplate(string moduleId, WorkflowDefinition template);

    void UnregisterModule(string moduleId);
}

/// <summary>Everything the host offers to modules (implemented by the host's application context).</summary>
public interface IModuleHost : ICommandContext, IIconContext, INavigationContext, IDockContext, ISecurityContext, INotificationContext
{
    ModuleLoader Modules { get; }

    IWorkflowRegistrar? WorkflowRegistrar { get; }
}
