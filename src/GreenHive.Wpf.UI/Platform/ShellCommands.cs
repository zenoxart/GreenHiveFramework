using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.UI.Platform.Shell;
using System.Windows.Input;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>Registers the commands of the shell with the central command manager.</summary>
internal static class ShellCommands
{
    private const string Module = ShortcutManager.ShellModuleId;
    private const string Menu = "shell.mainmenu";

    public static void Register(ClientAppContext context, Func<ShellWindow?> mainWindow, Func<StartupSequence?> startup)
    {
        var pool = context.Commands;
        var dock = context.Dock;

        pool.RegisterGroup(Module, new CommandGroupDescriptor(Menu, "file") { TextKey = "shell:menu.file", Order = 0 });
        pool.RegisterGroup(Module, new CommandGroupDescriptor(Menu, "view") { TextKey = "shell:menu.view", Order = 1 });
        pool.RegisterGroup(Module, new CommandGroupDescriptor(Menu, "window") { TextKey = "shell:menu.window", Order = 2 });
        pool.RegisterGroup(Module, new CommandGroupDescriptor(Menu, "help") { TextKey = "shell:menu.help", Order = 3 });
        pool.RegisterGroup(Module, new CommandGroupDescriptor("shell.navigation", "general") { TextKey = "shell:group.general", Order = 0 });
        pool.RegisterGroup(Module, new CommandGroupDescriptor("shell.navigation", "workflow") { TextKey = "shell:group.workflow", Order = 1 });
        pool.RegisterGroup(Module, new CommandGroupDescriptor("shell.navigation", ClassicViews.Group) { TextKey = "greenhive:group.classic", Order = 2 });

        var backend = context.Backend;
        Add(pool, "shell.signin", "shell:command.signin", "signin", [new(Menu, "file", 0, 0)], [],
            parameter => _ = startup()?.SignInAsync(),
            _ => !backend.IsSignedIn);

        Add(pool, "shell.signout", "shell:command.signout", "signout", [new(Menu, "file", 0, 1)], [],
            _ => backend.SignOut(),
            _ => backend.IsSignedIn);

        Add(pool, "shell.modules.reload", "shell:command.reloadmodules", "refresh", [new(Menu, "file", 1, 0)], [],
            parameter => _ = context.Modules.LoadAllAsync());

        Add(pool, "shell.updates.apply", "shell:command.applyupdates", "sync", [new(Menu, "file", 1, 1)], [],
            parameter => _ = context.HotSwap.ApplyPendingAsync(),
            _ => context.HotSwap.Pending.Count > 0);

        Add(pool, "shell.notifications.open", "shell:command.notifications", "notifications", [new(Menu, "view", 4)], ["Ctrl+Shift+N"],
            _ => context.Navigation.NavigateTo(BuiltInViews.Notifications));

        Add(pool, "shell.exit", "shell:command.exit", "power", [new(Menu, "file", 9)], [],
            _ => mainWindow()?.Close());

        Add(pool, "shell.palette", "shell:command.palette", "search", [new(Menu, "view", 2)], ["Ctrl+Shift+P"],
            _ => mainWindow()?.FocusPalette());

        Add(pool, "shell.navigationwheel.show", "shell:command.wheel", "switch", [new(Menu, "window", 0)], ["Ctrl+Tab", "Ctrl+Shift+Tab"],
            _ => mainWindow()?.BeginWheel(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)));

        Add(pool, "shell.view.detach", "shell:command.detach", "detach", [new(Menu, "window", 1, 0)], ["Ctrl+Shift+D"],
            _ => dock.Float(dock.ActiveItem),
            _ => dock.ActiveItem is { Location: not DockSide.Floating, Descriptor.CanFloat: true });

        Add(pool, "shell.view.attach", "shell:command.attach", "attach", [new(Menu, "window", 1, 1)], ["Ctrl+Shift+A"],
            _ => dock.Dock(dock.ActiveItem is { Location: DockSide.Floating } active ? active : dock.LastActiveFloating),
            _ => dock.LastActiveFloating is not null || dock.ActiveItem is { Location: DockSide.Floating });

        Add(pool, "shell.document.close", "shell:command.closedocument", "close", [new(Menu, "window", 1, 2)], ["Ctrl+F4"],
            _ => dock.Close(dock.ActiveDocument),
            _ => dock.ActiveDocument is { Descriptor.CanClose: true });

        Add(pool, "shell.document.next", "shell:command.nextdocument", "forward", [new(Menu, "window", 1, 3)], ["Ctrl+F6"],
            _ => dock.NextDocument());

        Add(pool, "shell.document.previous", "shell:command.previousdocument", "back", [new(Menu, "window", 1, 4)], ["Ctrl+Shift+F6"],
            _ => dock.PreviousDocument());

        AddToggle(pool, "shell.pane.left", "shell:command.toggleleft", "dock.left", [new(Menu, "window", 2, 0)],
            _ => dock.TogglePane(DockSide.Left), () => dock.IsPaneVisible(DockSide.Left));
        AddToggle(pool, "shell.pane.right", "shell:command.toggleright", "dock.right", [new(Menu, "window", 2, 1)],
            _ => dock.TogglePane(DockSide.Right), () => dock.IsPaneVisible(DockSide.Right));
        AddToggle(pool, "shell.pane.bottom", "shell:command.togglebottom", "dock.bottom", [new(Menu, "window", 2, 2)],
            _ => dock.TogglePane(DockSide.Bottom), () => dock.IsPaneVisible(DockSide.Bottom));

        Add(pool, "shell.layout.reset", "shell:command.resetlayout", "reset", [new(Menu, "window", 3)], [],
            _ => dock.ResetLayout());

        Add(pool, "shell.language.toggle", "shell:command.language", "language", [new(Menu, "view", 3)], [],
            _ =>
            {
                var next = context.Languages.Current.Equals("de", StringComparison.OrdinalIgnoreCase) ? "en" : "de";
                context.Languages.Set(next);
                context.Settings.Current.Language = next;
                context.Settings.Save();
            });

        Add(pool, "shell.about", "shell:command.about", "info", [new(Menu, "help", 0)], ["F1"],
            _ => mainWindow()?.ShowAbout());
        Add(pool, "shell.shortcuts.settings", "greenhive:command.shortcuts", "keyboard", [new(Menu, "view", 5, 0)], ["Ctrl+K, Ctrl+S"],
            _ => context.Navigation.NavigateTo(BuiltInViews.Settings));
        Add(pool, "shell.shortcuts.cheatsheet", "greenhive:command.cheatsheet", "help", [new(Menu, "help", 1)], ["Ctrl+/"],
            _ => mainWindow()?.ToggleCheatSheet());
        Add(pool, "shell.tutorial", "greenhive:command.tutorial", "help", [new(Menu, "help", 2)], ["F2"],
            _ => mainWindow()?.StartTutorial());
        Add(pool, "shell.heatmap", "greenhive:command.heatmap", "view", [new(Menu, "help", 3, 0)], [],
            _ => ClassicViews.ShowHeatmap());
        Add(pool, "shell.performance", "greenhive:command.performance", "output", [new(Menu, "help", 3, 1)], [],
            _ => ClassicViews.TogglePerformanceOverlay());

        // Enabled state of the window commands depends on the active view.
        dock.ActiveItemChanged += (_, _) => pool.RequeryAll();
        dock.LayoutChanged += (_, _) => pool.RequeryAll();
        backend.StateChanged += (_, _) => context.Dispatcher?.Run(pool.RequeryAll);
        context.HotSwap.PendingChanged += (_, _) => pool.Requery("shell.updates.apply");
    }

    private static void Add(RelayCommandPool pool, string id, string textKey, string icon, CommandPlacement[] placements, string[] shortcuts, Action<object?> execute, Func<object?, bool>? canExecute = null) =>
        pool.Register(Module, new CommandDescriptor(id)
        {
            TextKey = textKey,
            Icon = icon,
            Placements = placements,
            DefaultShortcuts = shortcuts.Select(s => ShortcutBinding.Parse(s)).ToList(),
        }, new DelegateCommandHandler(execute, canExecute));

    private static void AddToggle(RelayCommandPool pool, string id, string textKey, string icon, CommandPlacement[] placements, Action<object?> execute, Func<bool> isChecked) =>
        pool.Register(Module, new CommandDescriptor(id)
        {
            TextKey = textKey,
            Icon = icon,
            Kind = CommandKind.Toggle,
            Placements = placements,
        }, new DelegateCommandHandler(execute, null, _ => isChecked()));
}
