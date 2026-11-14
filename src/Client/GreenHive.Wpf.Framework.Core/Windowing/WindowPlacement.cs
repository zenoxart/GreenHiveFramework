namespace GreenHive.Wpf.Framework.Windowing;

/// <summary>Saved placement of one window, relative to the work area of the monitor it was on.</summary>
public sealed class WindowPlacement
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Outer bounds of the monitor (identifies it across sessions together with its size).</summary>
    public int MonitorX { get; set; }

    public int MonitorY { get; set; }

    public int MonitorWidth { get; set; }

    public int MonitorHeight { get; set; }

    /// <summary>Position relative to the monitor's work area, in physical pixels.</summary>
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Maximized { get; set; }
}

/// <summary>Content of WindowLayout.json.</summary>
public sealed class WindowLayoutFile
{
    public Dictionary<string, WindowPlacement> Windows { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
