using System;
using System.Windows.Input;

namespace GreenHive.Wpf.UI.Common.Shortcuts;

/// <summary>
/// Represents a keyboard shortcut with its associated action
/// </summary>
public class ShortcutAction
{
    /// <summary>
    /// Unique identifier for this shortcut
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the shortcut
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this shortcut does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for grouping shortcuts (e.g., "Navigation", "Editing", "View")
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// The keyboard shortcut (e.g., "Ctrl+S")
    /// </summary>
    public KeyGesture? KeyGesture { get; set; }

    /// <summary>
    /// The action to execute when the shortcut is triggered
    /// </summary>
    public Action? Action { get; set; }

    /// <summary>
    /// Command to execute (alternative to Action)
    /// </summary>
    public ICommand? Command { get; set; }

    /// <summary>
    /// Command parameter
    /// </summary>
    public object? CommandParameter { get; set; }

    /// <summary>
    /// Whether this shortcut can be modified by the user
    /// </summary>
    public bool IsModifiable { get; set; } = true;

    /// <summary>
    /// Whether this shortcut is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The default key gesture (for reset functionality)
    /// </summary>
    public KeyGesture? DefaultKeyGesture { get; private set; }

    public ShortcutAction()
    {
    }

    public ShortcutAction(string id, string displayName, KeyGesture? keyGesture, Action action, string category = "General", string description = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Category = category;
        KeyGesture = keyGesture;
        DefaultKeyGesture = keyGesture;
        Action = action;
    }

    public ShortcutAction(string id, string displayName, KeyGesture? keyGesture, ICommand command, object? commandParameter = null, string category = "General", string description = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Category = category;
        KeyGesture = keyGesture;
        DefaultKeyGesture = keyGesture;
        Command = command;
        CommandParameter = commandParameter;
    }

    /// <summary>
    /// Executes the shortcut action
    /// </summary>
    public void Execute()
    {
        if (!IsEnabled) return;

        if (Command != null && Command.CanExecute(CommandParameter))
        {
            Command.Execute(CommandParameter);
        }
        else
        {
            Action?.Invoke();
        }
    }

    /// <summary>
    /// Resets the key gesture to its default value
    /// </summary>
    public void ResetToDefault()
    {
        KeyGesture = DefaultKeyGesture;
    }

    /// <summary>
    /// Creates a copy of this shortcut action
    /// </summary>
    public ShortcutAction Clone()
    {
        return new ShortcutAction
        {
            Id = Id,
            DisplayName = DisplayName,
            Description = Description,
            Category = Category,
            KeyGesture = KeyGesture,
            DefaultKeyGesture = DefaultKeyGesture,
            Action = Action,
            Command = Command,
            CommandParameter = CommandParameter,
            IsModifiable = IsModifiable,
            IsEnabled = IsEnabled
        };
    }
}
