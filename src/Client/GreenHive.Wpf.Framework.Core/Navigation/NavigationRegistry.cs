using System.Collections.Concurrent;
using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>
/// Directory of all navigation targets. Every target is also registered as command "navigate.&lt;key&gt;"
/// with placements in the navigation tree, the navigation wheel (module homes) and the View menu,
/// so all menus are command trees.
/// </summary>
public sealed class NavigationRegistry : AppObject
{
    public const string NavigationPlacement = "shell.navigation";
    public const string WheelPlacement = "shell.wheel";
    public const string MainMenuPlacement = "shell.mainmenu";

    private readonly ConcurrentDictionary<string, NavigationTarget> _targets = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<NavigationTarget> All => _targets.Values.ToList();

    public IReadOnlyList<NavigationTarget> ModuleHomes => _targets.Values.Where(t => t.IsModuleHome).OrderBy(t => t.Order).ToList();

    public NavigationTarget? Find(string key) => _targets.TryGetValue(key, out var target) ? target : null;

    public void Register(NavigationTarget target)
    {
        _targets[target.Key] = target;
        var placements = new List<CommandPlacement> { new(NavigationPlacement, target.Group ?? target.ModuleId, 0, target.Order) };
        if (target.IsModuleHome)
        {
            placements.Add(new CommandPlacement(WheelPlacement, string.Empty, 0, target.Order));
        }

        if (target.ShowInViewMenu)
        {
            placements.Add(new CommandPlacement(MainMenuPlacement, "view", target.View.Role == ViewRole.ToolWindow ? 1 : 0, target.Order));
        }

        var descriptor = new CommandDescriptor(target.CommandId)
        {
            TextKey = target.TextKey,
            Icon = target.Icon,
            Permission = target.Permission,
            Placements = placements,
            WorkflowCapable = true,
        };
        var handler = new DelegateCommandHandler(parameter => Context<INavigationContext>().Navigation.NavigateTo(target.Key, parameter as string));
        Context<ICommandContext>().Commands.Register(target.ModuleId, descriptor, handler);
    }

    public void UnregisterModule(string moduleId)
    {
        foreach (var target in _targets.Values.Where(t => t.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _targets.TryRemove(target.Key, out _);
        }
    }
}
