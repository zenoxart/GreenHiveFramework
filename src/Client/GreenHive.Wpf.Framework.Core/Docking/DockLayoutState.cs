namespace GreenHive.Wpf.Framework.Docking;

/// <summary>Serializable dock layout (DockLayout.json). Sizes are stored per pane; positions of floating windows live in WindowLayout.json.</summary>
public sealed class DockLayoutState
{
    public int Version { get; set; } = 1;

    public double LeftWidth { get; set; } = 280;

    public double RightWidth { get; set; } = 300;

    public double BottomHeight { get; set; } = 220;

    public bool LeftShown { get; set; } = true;

    public bool RightShown { get; set; } = true;

    public bool BottomShown { get; set; } = true;

    /// <summary>False = the pane is in auto-hide mode (collapsed to its edge strip).</summary>
    public bool LeftPinned { get; set; } = true;

    public bool RightPinned { get; set; } = true;

    public bool BottomPinned { get; set; } = true;

    public List<DockedItemState> Items { get; set; } = [];

    public string? ActiveDocument { get; set; }
}

/// <summary>One open item of a saved layout.</summary>
public sealed class DockedItemState
{
    public string NavigationKey { get; set; } = string.Empty;

    public string? Parameter { get; set; }

    /// <summary>DocumentWell, Left, Right, Bottom or Floating.</summary>
    public string Location { get; set; } = "DocumentWell";

    public string LastDockedSide { get; set; } = "DocumentWell";
}
