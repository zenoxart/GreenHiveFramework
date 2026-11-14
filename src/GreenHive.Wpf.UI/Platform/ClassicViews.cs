using System.Windows;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Navigation;
using GreenHive.Wpf.Views;
using GreenHive.Wpf.Views.Hilfe;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>
/// The views of the classic Green Hive WPF framework (dashboard, settings, profile, administration, help) registered as
/// built-in dock views, so they live in the dock layout, the navigation tree and the navigation wheel like module views.
/// </summary>
internal static class ClassicViews
{
    public const string Group = "classic";

    public static void Register(NavigationRegistry registry)
    {
        Add(registry, "classic.dashboard", "greenhive:view.dashboard", "home", 0, () => new DashboardView(), home: true);
        Add(registry, "classic.settings", "greenhive:view.settings", "settings", 1, () => new EinstellungenView());
        Add(registry, "classic.profile", "greenhive:view.profile", "signin", 2, () => new ProfilView());
        Add(registry, "classic.admin", "greenhive:view.admin", "apps", 3, () => new AdminView(), permission: "users.edit");
        Add(registry, "classic.news", "greenhive:view.news", "info", 4, () => new NeuigkeitenView());
        Add(registry, "classic.glossary", "greenhive:view.glossary", "library", 5, () => new GlossarView());
        Add(registry, "classic.about", "greenhive:view.about", "help", 6, () => new AboutView());
    }

    /// <summary>Opens the interaction heatmap (classic diagnostics) in its own window.</summary>
    public static void ShowHeatmap()
    {
        var window = new Window
        {
            Title = "Interaction Heatmap",
            Width = 1000,
            Height = 700,
            Content = new GreenHive.Wpf.Controls.Common.Heatmap.InteractionHeatmapView(),
            Owner = Application.Current?.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        window.Show();
    }

    /// <summary>Shows or hides the classic performance overlay window.</summary>
    public static void TogglePerformanceOverlay()
    {
        if (_performance is { IsVisible: true })
        {
            _performance.Close();
            _performance = null;
            return;
        }

        _performance = new Window
        {
            Title = "Performance",
            Width = 420,
            Height = 360,
            Topmost = true,
            Content = new GreenHive.Wpf.Controls.Common.PerformanceMonitor.PerformancePanelView(),
            Owner = Application.Current?.MainWindow,
        };
        _performance.Show();
    }

    private static Window? _performance;

    private static void Add(NavigationRegistry registry, string key, string textKey, string icon, int order, Func<FrameworkElement> view,
        bool home = false, string? permission = null) =>
        registry.Register(new NavigationTarget(key, "classic")
        {
            TextKey = textKey,
            Icon = icon,
            Group = Group,
            Order = order,
            IsModuleHome = home,
            ShowInViewMenu = true,
            Permission = permission,
            View = new ViewDescriptor { Role = ViewRole.Document, MinWidth = 480, MinHeight = 360 },
            CreateViewModel = c => c.Produce<ClassicViewModel>(),
            CreateView = _ => view(),
        });
}

/// <summary>Placeholder ViewModel for the classic views (they bring their own DataContext).</summary>
public sealed class ClassicViewModel : ViewModelAppObject
{
}
