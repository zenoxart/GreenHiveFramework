namespace GreenHive.NET.ModuleContracts;

/// <summary>Role of a view in the dock layout.</summary>
public enum ViewRole
{
    /// <summary>Opens as a tab in the document well.</summary>
    Document,

    /// <summary>Docks at a side of the window (left, right, bottom) or floats.</summary>
    ToolWindow,
}

/// <summary>Dock positions.</summary>
public enum DockSide
{
    DocumentWell,
    Left,
    Right,
    Bottom,
    Floating,
}

/// <summary>How a view takes part in the dock layout.</summary>
public sealed record ViewDescriptor
{
    public ViewRole Role { get; init; } = ViewRole.Document;

    public DockSide DefaultDock { get; init; } = DockSide.DocumentWell;

    /// <summary>Documents only: several instances with different parameters may be open.</summary>
    public bool AllowMultipleInstances { get; init; }

    public bool CanClose { get; init; } = true;

    public bool CanFloat { get; init; } = true;

    /// <summary>Minimum size of the view's floating window, in effective pixels.</summary>
    public int MinWidth { get; init; } = 320;

    public int MinHeight { get; init; } = 240;
}
