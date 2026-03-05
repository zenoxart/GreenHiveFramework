using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace Zenox.Wpf.UI.Common.Shortcuts;

/// <summary>
/// Manages keyboard shortcuts for the application
/// </summary>
public class ShortcutManager
{
    private static ShortcutManager? _instance;
    private static readonly object _lock = new();

    public static ShortcutManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ShortcutManager();
                }
            }
            return _instance;
        }
    }

    private readonly Dictionary<string, ShortcutAction> _shortcuts = new();
    private readonly Dictionary<KeyGesture, ShortcutAction> _gestureMap = new(new KeyGestureComparer());
    private string _configurationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Zenox", "shortcuts.json");

    public IReadOnlyCollection<ShortcutAction> RegisteredShortcuts => _shortcuts.Values.ToList();

    public event EventHandler<ShortcutChangedEventArgs>? ShortcutChanged;
    public event EventHandler? ShortcutsLoaded;

    private ShortcutManager()
    {
    }

    /// <summary>
    /// Sets the path where shortcut configuration will be saved/loaded
    /// </summary>
    public void SetConfigurationPath(string path)
    {
        _configurationPath = path;
    }

    /// <summary>
    /// Registers a new shortcut
    /// </summary>
    public void RegisterShortcut(ShortcutAction shortcut)
    {
        if (string.IsNullOrEmpty(shortcut.Id))
            throw new ArgumentException("Shortcut must have an Id", nameof(shortcut));

        if (_shortcuts.ContainsKey(shortcut.Id))
        {
            UnregisterShortcut(shortcut.Id);
        }

        _shortcuts[shortcut.Id] = shortcut;

        if (shortcut.KeyGesture != null)
        {
            _gestureMap[shortcut.KeyGesture] = shortcut;
        }

        ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs(shortcut, ShortcutChangeType.Registered));
    }

    /// <summary>
    /// Unregisters a shortcut
    /// </summary>
    public void UnregisterShortcut(string id)
    {
        if (_shortcuts.TryGetValue(id, out var shortcut))
        {
            if (shortcut.KeyGesture != null)
            {
                _gestureMap.Remove(shortcut.KeyGesture);
            }
            _shortcuts.Remove(id);
            ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs(shortcut, ShortcutChangeType.Unregistered));
        }
    }

    /// <summary>
    /// Gets a shortcut by its ID
    /// </summary>
    public ShortcutAction? GetShortcut(string id)
    {
        return _shortcuts.TryGetValue(id, out var shortcut) ? shortcut : null;
    }

    /// <summary>
    /// Gets all shortcuts in a specific category
    /// </summary>
    public IEnumerable<ShortcutAction> GetShortcutsByCategory(string category)
    {
        return _shortcuts.Values.Where(s => s.Category == category);
    }

    /// <summary>
    /// Gets all unique categories
    /// </summary>
    public IEnumerable<string> GetCategories()
    {
        return _shortcuts.Values.Select(s => s.Category).Distinct().OrderBy(c => c);
    }

    /// <summary>
    /// Updates the key gesture for a shortcut
    /// </summary>
    public bool UpdateShortcutKey(string id, KeyGesture? newGesture)
    {
        if (!_shortcuts.TryGetValue(id, out var shortcut))
            return false;

        if (!shortcut.IsModifiable)
            return false;

        // Check if new gesture is already in use
        if (newGesture != null && IsGestureInUse(newGesture, out var existingShortcut) && existingShortcut?.Id != id)
        {
            throw new InvalidOperationException($"Shortcut already in use by: {existingShortcut?.DisplayName}");
        }

        // Remove old gesture mapping
        if (shortcut.KeyGesture != null)
        {
            _gestureMap.Remove(shortcut.KeyGesture);
        }

        // Update shortcut
        shortcut.KeyGesture = newGesture;

        // Add new gesture mapping
        if (newGesture != null)
        {
            _gestureMap[newGesture] = shortcut;
        }

        ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs(shortcut, ShortcutChangeType.Modified));
        return true;
    }

    /// <summary>
    /// Checks if a key gesture is already in use
    /// </summary>
    public bool IsGestureInUse(KeyGesture gesture, out ShortcutAction? existingShortcut)
    {
        return _gestureMap.TryGetValue(gesture, out existingShortcut);
    }

    /// <summary>
    /// Handles a key press and executes the associated shortcut if found
    /// </summary>
    public bool HandleKeyPress(Key key, ModifierKeys modifiers)
    {
        var gesture = new KeyGesture(key, modifiers);
        
        if (_gestureMap.TryGetValue(gesture, out var shortcut) && shortcut.IsEnabled)
        {
            try
            {
                shortcut.Execute();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing shortcut {shortcut.Id}: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Saves the current shortcut configuration to disk
    /// </summary>
    public void SaveConfiguration()
    {
        try
        {
            var config = new ShortcutConfiguration
            {
                LastModified = DateTime.Now,
                Shortcuts = _shortcuts.Values.Select(s => new ShortcutEntry
                {
                    Id = s.Id,
                    KeyGestureText = s.KeyGesture?.GetDisplayStringForCulture(System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty,
                    IsEnabled = s.IsEnabled,
                    Category = s.Category
                }).ToList()
            };

            var directory = Path.GetDirectoryName(_configurationPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_configurationPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving shortcuts: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads shortcut configuration from disk
    /// </summary>
    public void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configurationPath))
                return;

            var json = File.ReadAllText(_configurationPath);
            var config = JsonSerializer.Deserialize<ShortcutConfiguration>(json);

            if (config == null)
                return;

            foreach (var entry in config.Shortcuts)
            {
                if (_shortcuts.TryGetValue(entry.Id, out var shortcut))
                {
                    shortcut.IsEnabled = entry.IsEnabled;

                    if (!string.IsNullOrEmpty(entry.KeyGestureText))
                    {
                        try
                        {
                            var converter = new KeyGestureConverter();
                            if (converter.ConvertFromString(entry.KeyGestureText) is KeyGesture gesture)
                            {
                                UpdateShortcutKey(entry.Id, gesture);
                            }
                        }
                        catch
                        {
                            // Keep default if conversion fails
                        }
                    }
                }
            }

            ShortcutsLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading shortcuts: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets all shortcuts to their default values
    /// </summary>
    public void ResetAllToDefaults()
    {
        foreach (var shortcut in _shortcuts.Values)
        {
            if (shortcut.IsModifiable)
            {
                shortcut.ResetToDefault();
            }
        }

        RebuildGestureMap();
        ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs(null, ShortcutChangeType.Reset));
    }

    /// <summary>
    /// Resets a specific shortcut to its default value
    /// </summary>
    public void ResetToDefault(string id)
    {
        if (_shortcuts.TryGetValue(id, out var shortcut) && shortcut.IsModifiable)
        {
            // Remove old gesture
            if (shortcut.KeyGesture != null)
            {
                _gestureMap.Remove(shortcut.KeyGesture);
            }

            // Reset to default
            shortcut.ResetToDefault();

            // Add new gesture
            if (shortcut.KeyGesture != null)
            {
                _gestureMap[shortcut.KeyGesture] = shortcut;
            }

            ShortcutChanged?.Invoke(this, new ShortcutChangedEventArgs(shortcut, ShortcutChangeType.Modified));
        }
    }

    /// <summary>
    /// Rebuilds the internal gesture map
    /// </summary>
    private void RebuildGestureMap()
    {
        _gestureMap.Clear();
        foreach (var shortcut in _shortcuts.Values)
        {
            if (shortcut.KeyGesture != null)
            {
                _gestureMap[shortcut.KeyGesture] = shortcut;
            }
        }
    }

    /// <summary>
    /// Clears all registered shortcuts
    /// </summary>
    public void Clear()
    {
        _shortcuts.Clear();
        _gestureMap.Clear();
    }

    /// <summary>
    /// Custom comparer for KeyGesture to use in Dictionary
    /// </summary>
    private class KeyGestureComparer : IEqualityComparer<KeyGesture>
    {
        public bool Equals(KeyGesture? x, KeyGesture? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.Key == y.Key && x.Modifiers == y.Modifiers;
        }

        public int GetHashCode(KeyGesture obj)
        {
            return HashCode.Combine(obj.Key, obj.Modifiers);
        }
    }
}

public class ShortcutChangedEventArgs : EventArgs
{
    public ShortcutAction? Shortcut { get; }
    public ShortcutChangeType ChangeType { get; }

    public ShortcutChangedEventArgs(ShortcutAction? shortcut, ShortcutChangeType changeType)
    {
        Shortcut = shortcut;
        ChangeType = changeType;
    }
}

public enum ShortcutChangeType
{
    Registered,
    Unregistered,
    Modified,
    Reset
}
