using System;
using System.Collections.Generic;

namespace GreenHive.Wpf.UI.Common.Shortcuts;

/// <summary>
/// Configuration data for shortcuts that can be serialized/deserialized
/// </summary>
[Serializable]
public class ShortcutConfiguration
{
    public List<ShortcutEntry> Shortcuts { get; set; } = new();

    public DateTime LastModified { get; set; } = DateTime.Now;

    public string Version { get; set; } = "1.0";
}

/// <summary>
/// Serializable representation of a shortcut
/// </summary>
[Serializable]
public class ShortcutEntry
{
    public string Id { get; set; } = string.Empty;
    
    public string KeyGestureText { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;

    public string Category { get; set; } = "General";
}
