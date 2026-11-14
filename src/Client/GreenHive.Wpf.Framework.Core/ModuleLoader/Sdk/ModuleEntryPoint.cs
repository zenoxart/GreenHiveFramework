using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>
/// Base class of module entry points: keeps the module's ModuleAppContext and offers short registration helpers.
/// Everything registered here is removed automatically when the module unloads.
/// </summary>
public abstract class ModuleEntryPoint : IModuleEntryPoint
{
    protected ModuleAppContext Module { get; private set; } = null!;

    public void Initialize(IModuleContext context)
    {
        Module = (ModuleAppContext)context;
        OnInitialize();
    }

    public virtual object? GetXamlMetadataProvider() => null;

    public virtual Task ShutdownAsync() => Task.CompletedTask;

    protected abstract void OnInitialize();

    protected void Texts(string language, IReadOnlyDictionary<string, string> texts) => Module.RegisterTexts(language, texts);

    protected void Icon(string name, string glyphHex) => Module.RegisterIcon(name, glyphHex);

    /// <summary>Registers a view: navigation target + dock role; the ViewModel is produced by the module context.</summary>
    protected void View<TViewModel>(string key, string textKey, string icon, Func<TViewModel, object> view, ViewDescriptor? descriptor = null,
        bool home = false, int order = 0, string? permission = null, bool viewMenu = true)
        where TViewModel : ViewModelAppObject, new() =>
        Module.RegisterView(new ModuleViewRegistration(key)
        {
            TextKey = textKey,
            Icon = icon,
            View = descriptor ?? new ViewDescriptor(),
            IsModuleHome = home,
            Group = Module.ModuleId,
            Order = order,
            Permission = permission,
            ShowInViewMenu = viewMenu,
            CreateViewModel = () => Module.Produce<TViewModel>(),
            CreateView = vm => view((TViewModel)vm),
        });

    /// <summary>Registers a command with the central command manager.</summary>
    protected void Command(string id, string textKey, string icon, Func<object?, Task> execute, Func<object?, bool>? canExecute = null,
        string? permission = null, IReadOnlyList<CommandPlacement>? placements = null, IReadOnlyList<string>? shortcuts = null,
        bool workflowCapable = false, bool aiAllowed = false) =>
        Module.RegisterCommand(new CommandDescriptor(id)
        {
            TextKey = textKey,
            Icon = icon,
            Permission = permission,
            Placements = placements ?? [],
            DefaultShortcuts = shortcuts?.Select(s => ShortcutBinding.Parse(s, ShortcutScope.Module, Module.ModuleId)).ToList() ?? [],
            WorkflowCapable = workflowCapable,
            AiAllowed = aiAllowed,
        }, new DelegateCommandHandler((parameter, _) => execute(parameter), canExecute));

    /// <summary>Placement in the module's own main-menu group (created by <see cref="MenuGroup"/>).</summary>
    protected CommandPlacement InMenu(int section = 0, int order = 0) => new("shell.mainmenu", Module.Prefix, section, order);

    protected void MenuGroup(string textKey, int order = 10) =>
        Module.RegisterGroup(new CommandGroupDescriptor("shell.mainmenu", Module.Prefix) { TextKey = textKey, Order = order });

    /// <summary>Titled group of the module's views in the navigation tree (views are placed in the group named after the module ID).</summary>
    protected void NavigationGroup(string textKey, int order = 10) =>
        Module.RegisterGroup(new CommandGroupDescriptor("shell.navigation", Module.ModuleId) { TextKey = textKey, Order = order });
}
