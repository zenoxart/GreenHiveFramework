using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>A view that can be navigated to (registered by the host or a module).</summary>
public sealed record NavigationTarget(string Key, string ModuleId)
{
    /// <summary>Base text key; the title is "&lt;TextKey&gt;.label".</summary>
    public string? TextKey { get; init; }

    public string? Icon { get; init; }

    /// <summary>Dock role and default position.</summary>
    public ViewDescriptor View { get; init; } = new();

    /// <summary>The module's main view: its segment in the navigation wheel.</summary>
    public bool IsModuleHome { get; init; }

    /// <summary>Folder in the navigation tree (Module Explorer), e.g. "general".</summary>
    public string? Group { get; init; }

    public int Order { get; init; }

    /// <summary>Also listed in the main menu's View menu.</summary>
    public bool ShowInViewMenu { get; init; }

    public string? Permission { get; init; }

    public required Func<ApplicationContext, ViewModelAppObject> CreateViewModel { get; init; }

    /// <summary>Creates the view (a WPF FrameworkElement) for the ViewModel.</summary>
    public required Func<ViewModelAppObject, object> CreateView { get; init; }

    /// <summary>Command that opens the target ("navigate.&lt;key&gt;").</summary>
    public string CommandId => "navigate." + Key;
}
