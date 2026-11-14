using GreenHive.Wpf.UI.Platform.Views;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Navigation;
using GreenHive.Wpf.Framework.Workflows;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>Registers the built-in views of the shell and the workflow views (modules register their own views when loaded).</summary>
internal static class BuiltInViews
{
    public const string Start = "shell.start";
    public const string Settings = "shell.settings";
    public const string ModuleExplorer = "shell.moduleexplorer";
    public const string Output = "shell.output";
    public const string Notifications = "shell.notifications";

    /// <summary>Views opened when no saved layout exists.</summary>
    public static readonly IReadOnlyList<string> DefaultLayout = new string[] { Start, ModuleExplorer, Output, WorkflowViews.Inbox, WorkflowViews.Ai };

    public static void Register(ClientAppContext context)
    {
        var registry = context.Navigation.Registry;

        registry.Register(new NavigationTarget(Start, "shell")
        {
            TextKey = "shell:view.start",
            Icon = "home",
            Group = "general",
            Order = 0,
            IsModuleHome = true,
            ShowInViewMenu = true,
            View = new ViewDescriptor { Role = ViewRole.Document },
            CreateViewModel = c => c.Produce<StartViewModel>(),
            CreateView = vm => new StartView((StartViewModel)vm),
        });

        registry.Register(new NavigationTarget(Settings, "shell")
        {
            TextKey = "shell:view.settings",
            Icon = "settings",
            Group = "general",
            Order = 9,
            ShowInViewMenu = true,
            View = new ViewDescriptor { Role = ViewRole.Document, MinWidth = 480, MinHeight = 400 },
            CreateViewModel = c => c.Produce<SettingsViewModel>(),
            CreateView = vm => new SettingsView((SettingsViewModel)vm),
        });

        registry.Register(new NavigationTarget(ModuleExplorer, "shell")
        {
            TextKey = "shell:view.moduleexplorer",
            Icon = "library",
            Group = "general",
            Order = 1,
            ShowInViewMenu = true,
            View = new ViewDescriptor { Role = ViewRole.ToolWindow, DefaultDock = DockSide.Left, CanClose = true },
            CreateViewModel = c => c.Produce<ModuleExplorerViewModel>(),
            CreateView = vm => new ModuleExplorerView((ModuleExplorerViewModel)vm),
        });

        registry.Register(new NavigationTarget(Output, "shell")
        {
            TextKey = "shell:view.output",
            Icon = "output",
            Group = "general",
            Order = 2,
            ShowInViewMenu = true,
            View = new ViewDescriptor { Role = ViewRole.ToolWindow, DefaultDock = DockSide.Bottom },
            CreateViewModel = c => c.Produce<OutputViewModel>(),
            CreateView = vm => new OutputView((OutputViewModel)vm),
        });

        registry.Register(new NavigationTarget(Notifications, "shell")
        {
            TextKey = "shell:view.notifications",
            Icon = "notifications",
            Group = "general",
            Order = 3,
            ShowInViewMenu = true,
            View = new ViewDescriptor { Role = ViewRole.ToolWindow, DefaultDock = DockSide.Right, MinWidth = 280 },
            CreateViewModel = c => c.Produce<NotificationsViewModel>(),
            CreateView = vm => new NotificationsView((NotificationsViewModel)vm),
        });

        WorkflowViewRegistration.Register(registry);
        ClassicViews.Register(registry);
    }
}
