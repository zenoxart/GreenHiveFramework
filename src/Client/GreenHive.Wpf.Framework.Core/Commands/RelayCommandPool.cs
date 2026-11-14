using System.Collections.Concurrent;
using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>A command found by the command palette search.</summary>
public sealed record CommandSearchResult(string CommandId, string Label, string? ShortcutText, bool IsEnabled)
{
    public override string ToString() => ShortcutText is null ? Label : $"{Label}    ({ShortcutText})";
}

/// <summary>
/// The central command manager of the application. Every command - from the host or a module - is
/// registered, looked up, executed, enabled/disabled and arranged in menus through it. It owns the
/// UI dispatcher and the shortcut manager and replaces WPF's CommandManager, which the framework replaces with one pool for host and modules.
/// </summary>
public sealed class RelayCommandPool : AppObject
{
    private readonly ConcurrentDictionary<string, PooledCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (ICommandHandler Handler, EventHandler Subscription)> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingTreeChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private CommandExecutor? _executor;
    private ShortcutManager? _shortcuts;
    private bool _treeChangePosted;

    /// <summary>Raised (coalesced, on the UI thread) when the tree of a placement may have changed; "*" = all.</summary>
    public event EventHandler<string>? TreeChanged;

    /// <summary>The application's UI dispatcher (one queue per window).</summary>
    public CommandDispatcher Dispatcher { get; } = new();

    public DynamicCommandRegistry Registry { get; } = new();

    public CommandExecutor Executor
    {
        get
        {
            lock (_sync)
            {
                return _executor ??= AppContext.Produce<CommandExecutor>();
            }
        }
    }

    /// <summary>The shortcut manager owned by the pool.</summary>
    public ShortcutManager Shortcuts
    {
        get
        {
            lock (_sync)
            {
                return _shortcuts ??= AppContext.Produce<ShortcutManager>();
            }
        }
    }

    /// <summary>Registers (or, for the same module, replaces) a command.</summary>
    public bool Register(string moduleId, CommandDescriptor descriptor, ICommandHandler handler)
    {
        if (!Registry.TryRegister(moduleId, descriptor, handler, out var conflictingModule))
        {
            AppContext.Log.Write($"Command '{descriptor.Id}' of '{moduleId}' rejected: already registered by '{conflictingModule}'.", LogEntryType.Warning, nameof(RelayCommandPool));
            return false;
        }

        Unsubscribe(descriptor.Id);
        EventHandler subscription = (_, _) => Requery(descriptor.Id);
        handler.CanExecuteChanged += subscription;
        _subscriptions[descriptor.Id] = (handler, subscription);

        Shortcuts.Invalidate();
        Requery(descriptor.Id);
        foreach (var placement in descriptor.Placements)
        {
            RaiseTreeChanged(placement.Placement);
        }

        return true;
    }

    /// <summary>Registers a group descriptor (label, icon, order of a submenu or tree folder).</summary>
    public void RegisterGroup(string moduleId, CommandGroupDescriptor group)
    {
        Registry.RegisterGroup(moduleId, group);
        RaiseTreeChanged(group.Placement);
    }

    /// <summary>Removes all commands of a module (unload).</summary>
    public void UnregisterModule(string moduleId)
    {
        foreach (var id in Registry.UnregisterModule(moduleId))
        {
            Unsubscribe(id);
            Requery(id);
        }

        Shortcuts.Invalidate();
        RaiseTreeChanged("*");
    }

    /// <summary>Returns the one stable command instance for the ID (also for IDs not registered yet).</summary>
    public PooledCommand Get(string commandId) => _commands.GetOrAdd(commandId, id => new PooledCommand(this, id));

    public CommandDescriptor? GetDescriptor(string commandId) => Registry.Find(commandId)?.Descriptor;

    public bool CanExecute(string commandId, object? parameter)
    {
        var registration = Registry.Find(commandId);
        if (registration is null || !Executor.HasPermission(registration.Descriptor.Permission))
        {
            return false;
        }

        try
        {
            return registration.Handler.CanExecute(parameter);
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex);
            return false;
        }
    }

    public bool? IsChecked(string commandId, object? parameter) => Registry.Find(commandId)?.Handler.IsChecked(parameter);

    /// <summary>Executes a command for callers without a binding (shortcuts, palette, deep links).</summary>
    public Task<bool> ExecuteAsync(string commandId, object? parameter = null)
    {
        var registration = Registry.Find(commandId);
        if (registration is null)
        {
            AppContext.Log.Write($"Unknown command '{commandId}'", LogEntryType.Warning, nameof(RelayCommandPool));
            return Task.FromResult(false);
        }

        return Executor.ExecuteAsync(registration, parameter);
    }

    /// <summary>Raises CanExecuteChanged for one command through the pool's dispatcher.</summary>
    public void Requery(string commandId)
    {
        if (_commands.TryGetValue(commandId, out var command))
        {
            command.RaiseCanExecuteChanged();
        }
    }

    public void RequeryAll()
    {
        foreach (var command in _commands.Values)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Builds the command tree for a placement (menu, tree view, wheel).</summary>
    public IReadOnlyList<CommandTreeNode> GetTree(string placement) =>
        CommandTreeBuilder.Build(
            placement,
            Registry.All,
            Registry.Groups,
            r => Executor.HasPermission(r.Descriptor.Permission),
            key => key is null ? string.Empty : GetText(key, "label") ?? key);

    /// <summary>Searches executable commands by label (command palette).</summary>
    public IReadOnlyList<CommandSearchResult> Find(string text, int max = 15)
    {
        var query = text.Trim();
        return Registry.All
            .Where(r => Executor.HasPermission(r.Descriptor.Permission))
            .Select(r => new CommandSearchResult(r.Descriptor.Id, GetLabel(r.Descriptor.Id), Shortcuts.GetGestureText(r.Descriptor.Id), CanExecute(r.Descriptor.Id, null)))
            .Where(r => query.Length == 0 || r.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.CommandId.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.IsEnabled)
            .ThenBy(r => r.Label.StartsWith(query, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(max)
            .ToList();
    }

    /// <summary>Localized label of a command (falls back to the ID).</summary>
    public string GetLabel(string commandId) =>
        GetDescriptor(commandId)?.TextKey is { } key ? GetText(key, "label") ?? AppContext.Languages.GetText(key + ".label") : commandId;

    /// <summary>Localized tooltip line of a command, if defined.</summary>
    public string? GetToolTipText(string commandId) =>
        GetDescriptor(commandId)?.TextKey is { } key ? GetText(key, "tooltip") : null;

    /// <summary>Text of the convention key "&lt;baseKey&gt;.&lt;suffix&gt;" without reporting it as missing.</summary>
    public string? GetText(string baseKey, string suffix) =>
        AppContext.Languages.TryGetText($"{baseKey}.{suffix}", out var text) ? text : null;

    protected override void OnCreated()
    {
        AppContext.Languages.LanguageChanged += (_, _) =>
        {
            Shortcuts.Invalidate();
            RaiseTreeChanged("*");
        };
    }

    private void Unsubscribe(string commandId)
    {
        if (_subscriptions.TryRemove(commandId, out var old))
        {
            old.Handler.CanExecuteChanged -= old.Subscription;
        }
    }

    private void RaiseTreeChanged(string placement)
    {
        lock (_pendingTreeChanges)
        {
            _pendingTreeChanges.Add(placement);
            if (_treeChangePosted)
            {
                return;
            }

            _treeChangePosted = true;
        }

        Dispatcher.Post(() =>
        {
            string[] placements;
            lock (_pendingTreeChanges)
            {
                placements = _pendingTreeChanges.Contains("*") ? ["*"] : _pendingTreeChanges.ToArray();
                _pendingTreeChanges.Clear();
                _treeChangePosted = false;
            }

            foreach (var changed in placements)
            {
                TreeChanged?.Invoke(this, changed);
            }
        }, DispatchPriority.Low);
    }
}
