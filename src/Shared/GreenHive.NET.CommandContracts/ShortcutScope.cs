namespace GreenHive.NET.CommandContracts;

/// <summary>Where a shortcut is active. Higher values are more specific and win.</summary>
public enum ShortcutScope
{
    Global = 0,
    Window = 1,
    Module = 2,
    View = 3,
    Focus = 4,
}

/// <summary>A default shortcut suggested by a command descriptor.</summary>
public sealed record ShortcutBinding(KeyGesture Gesture, ShortcutScope Scope = ShortcutScope.Global, string? ScopeKey = null)
{
    /// <summary>Creates a binding from gesture text, e.g. <c>ShortcutBinding.Parse("Ctrl+Shift+P")</c>.</summary>
    public static ShortcutBinding Parse(string gesture, ShortcutScope scope = ShortcutScope.Global, string? scopeKey = null) =>
        new(KeyGesture.Parse(gesture), scope, scopeKey);
}
