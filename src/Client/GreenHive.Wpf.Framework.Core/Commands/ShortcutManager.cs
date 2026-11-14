using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>Where an effective binding comes from.</summary>
public enum ShortcutSource
{
    Shell,
    Module,
    User,
}

/// <summary>One effective binding: gesture + scope -> command.</summary>
public sealed record ShortcutEntry(string CommandId, KeyGesture Gesture, ShortcutScope Scope, string? ScopeKey, ShortcutSource Source);

/// <summary>Two commands claimed the same gesture in the same scope; the first one won.</summary>
public sealed record ShortcutConflict(string Gesture, string WinningCommandId, string LosingCommandId);

/// <summary>
/// The shortcut manager, owned by the RelayCommandPool. Combines shell defaults, module defaults and
/// user overrides (roaming) into the effective binding table, detects conflicts, protects reserved shell
/// shortcuts from modules and resolves key presses in the active scopes.
/// </summary>
public sealed class ShortcutManager : AppObject
{
    /// <summary>Module that owns the shell commands; only it may use reserved gestures.</summary>
    public const string ShellModuleId = "shell";

    /// <summary>Gestures reserved for the shell (modules cannot claim them; the user may remap them).</summary>
    public static readonly IReadOnlySet<string> ReservedGestures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Ctrl+Tab", "Ctrl+Shift+Tab", "Ctrl+Shift+P", "Ctrl+K, Ctrl+S", "F1", "Esc",
    };

    private readonly object _sync = new();
    private Dictionary<string, List<string>>? _overrides;
    private List<ShortcutEntry> _effective = [];
    private Dictionary<string, List<ShortcutEntry>> _byGesture = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _chordPrefixes = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ShortcutConflict> _conflicts = [];
    private bool _dirty = true;
    private bool _changePosted;

    /// <summary>Raised (coalesced, on the UI thread) after the effective bindings changed.</summary>
    public event EventHandler? BindingsChanged;

    /// <summary>The currently active scopes besides Global, e.g. (View, "shell.settings"), (Module, "shell").</summary>
    public IReadOnlyList<(ShortcutScope Scope, string? Key)> ActiveScopes { get; set; } = [];

    public IReadOnlyList<ShortcutEntry> Effective
    {
        get
        {
            EnsureBuilt();
            return _effective;
        }
    }

    public IReadOnlyList<ShortcutConflict> Conflicts
    {
        get
        {
            EnsureBuilt();
            return _conflicts;
        }
    }

    private RelayCommandPool Pool => Context<ICommandContext>().Commands;

    private ShortcutProfileStore? _store;

    private ShortcutProfileStore Store => _store ??= AppContext.Produce<ShortcutProfileStore>();

    /// <summary>Marks the table for rebuild (commands, overrides or language changed).</summary>
    public void Invalidate()
    {
        lock (_sync)
        {
            _dirty = true;
            if (_changePosted)
            {
                return;
            }

            _changePosted = true;
        }

        Pool.Dispatcher.Post(() =>
        {
            lock (_sync)
            {
                _changePosted = false;
            }

            BindingsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Finds the command for a gesture in the most specific active scope.</summary>
    public string? Resolve(KeyGesture gesture)
    {
        EnsureBuilt();
        if (!_byGesture.TryGetValue(gesture.ToString(), out var entries))
        {
            return null;
        }

        var scopes = ActiveScopes;
        return entries
            .Where(e => e.Scope == ShortcutScope.Global || scopes.Any(s => s.Scope == e.Scope && (e.ScopeKey is null || string.Equals(e.ScopeKey, s.Key, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(e => e.Scope)
            .Select(e => e.CommandId)
            .FirstOrDefault();
    }

    /// <summary>True if the gesture is the first step of a chord.</summary>
    public bool IsChordPrefix(KeyGesture gesture)
    {
        EnsureBuilt();
        return _chordPrefixes.Contains(gesture.ToString());
    }

    public IReadOnlyList<KeyGesture> GetGestures(string commandId) =>
        Effective.Where(e => e.CommandId.Equals(commandId, StringComparison.OrdinalIgnoreCase)).Select(e => e.Gesture).ToList();

    /// <summary>Localized display text of the first gesture of a command ("Strg+Umschalt+P"), or null.</summary>
    public string? GetGestureText(string commandId)
    {
        var gesture = GetGestures(commandId).FirstOrDefault();
        return gesture is null ? null : Format(gesture);
    }

    /// <summary>Formats a gesture with localized modifier names.</summary>
    public string Format(KeyGesture gesture) =>
        gesture.ToDisplayString(m => AppContext.Languages.TryGetText($"shell:key.{m.ToString().ToLowerInvariant()}", out var text) ? text : m.ToString());

    public bool HasUserOverride(string commandId)
    {
        EnsureOverrides();
        lock (_sync)
        {
            return _overrides!.ContainsKey(commandId);
        }
    }

    /// <summary>Sets the user's gesture for a command; another command using it in the same scope loses it.</summary>
    public void SetUserGesture(string commandId, KeyGesture gesture)
    {
        EnsureBuilt();
        var key = gesture.ToString();
        var scope = Pool.GetDescriptor(commandId)?.DefaultShortcuts.FirstOrDefault()?.Scope ?? ShortcutScope.Global;
        lock (_sync)
        {
            foreach (var other in _effective.Where(e => e.Gesture.ToString().Equals(key, StringComparison.OrdinalIgnoreCase) && e.Scope == scope && !e.CommandId.Equals(commandId, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _overrides![other.CommandId] = _effective
                    .Where(e => e.CommandId.Equals(other.CommandId, StringComparison.OrdinalIgnoreCase) && !e.Gesture.ToString().Equals(key, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Gesture.ToString())
                    .ToList();
            }

            _overrides![commandId] = [key];
        }

        SaveOverrides();
    }

    public void ResetUserGesture(string commandId)
    {
        EnsureOverrides();
        lock (_sync)
        {
            _overrides!.Remove(commandId);
        }

        SaveOverrides();
    }

    public void ResetAll()
    {
        EnsureOverrides();
        lock (_sync)
        {
            _overrides!.Clear();
        }

        SaveOverrides();
    }

    private void SaveOverrides()
    {
        Dictionary<string, List<string>> copy;
        lock (_sync)
        {
            copy = new Dictionary<string, List<string>>(_overrides!, StringComparer.OrdinalIgnoreCase);
        }

        Store.Save(copy);
        Invalidate();
    }

    private void EnsureOverrides()
    {
        if (_overrides is not null)
        {
            return;
        }

        var loaded = Store.Load();
        lock (_sync)
        {
            _overrides ??= new Dictionary<string, List<string>>(loaded, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void EnsureBuilt()
    {
        if (!_dirty)
        {
            return;
        }

        EnsureOverrides();
        var effective = new List<ShortcutEntry>();
        var conflicts = new List<ShortcutConflict>();
        var claimed = new Dictionary<string, ShortcutEntry>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<string>> overrides;
        lock (_sync)
        {
            overrides = new Dictionary<string, List<string>>(_overrides!, StringComparer.OrdinalIgnoreCase);
        }

        // Shell first, then user overrides, then modules: the first claim of a gesture in a scope wins.
        var candidates = new List<ShortcutEntry>();
        foreach (var registration in Pool.Registry.All)
        {
            var id = registration.Descriptor.Id;
            var isShell = registration.ModuleId.Equals(ShellModuleId, StringComparison.OrdinalIgnoreCase);
            var defaultScope = registration.Descriptor.DefaultShortcuts.FirstOrDefault();
            if (overrides.TryGetValue(id, out var userGestures))
            {
                foreach (var text in userGestures)
                {
                    if (KeyGesture.TryParse(text, out var gesture) && gesture is not null)
                    {
                        candidates.Add(new ShortcutEntry(id, gesture, defaultScope?.Scope ?? ShortcutScope.Global, defaultScope?.ScopeKey, ShortcutSource.User));
                    }
                }

                continue;
            }

            foreach (var binding in registration.Descriptor.DefaultShortcuts)
            {
                if (!isShell && ReservedGestures.Contains(binding.Gesture.ToString()))
                {
                    AppContext.Log.Write($"Shortcut {binding.Gesture} of '{id}' rejected: reserved for the shell.", LogEntryType.Warning, nameof(ShortcutManager));
                    continue;
                }

                candidates.Add(new ShortcutEntry(id, binding.Gesture, binding.Scope, binding.ScopeKey, isShell ? ShortcutSource.Shell : ShortcutSource.Module));
            }
        }

        foreach (var candidate in candidates.OrderBy(c => c.Source switch { ShortcutSource.Shell => 0, ShortcutSource.User => 1, _ => 2 }))
        {
            var claimKey = $"{candidate.Gesture}|{candidate.Scope}|{candidate.ScopeKey}";
            if (claimed.TryGetValue(claimKey, out var winner))
            {
                if (!winner.CommandId.Equals(candidate.CommandId, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts.Add(new ShortcutConflict(candidate.Gesture.ToString(), winner.CommandId, candidate.CommandId));
                }

                continue;
            }

            claimed[claimKey] = candidate;
            effective.Add(candidate);
        }

        var byGesture = new Dictionary<string, List<ShortcutEntry>>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in effective)
        {
            var key = entry.Gesture.ToString();
            if (!byGesture.TryGetValue(key, out var list))
            {
                list = [];
                byGesture[key] = list;
            }

            list.Add(entry);
            if (entry.Gesture.IsChord)
            {
                prefixes.Add(entry.Gesture.FirstPart.ToString());
            }
        }

        lock (_sync)
        {
            _effective = effective;
            _byGesture = byGesture;
            _chordPrefixes = prefixes;
            _conflicts = conflicts;
            _dirty = false;
        }
    }
}
