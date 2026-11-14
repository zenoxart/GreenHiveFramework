namespace GreenHive.NET.CommandContracts;

/// <summary>
/// Describes a group node (submenu / tree folder) explicitly. Groups do not have to be
/// registered - missing groups are created implicitly; registering one adds label, icon and order.
/// </summary>
public sealed record CommandGroupDescriptor(string Placement, string Path)
{
    /// <summary>Base text key; the label is "&lt;TextKey&gt;.label".</summary>
    public string? TextKey { get; init; }

    public string? Icon { get; init; }

    public int Section { get; init; }

    public int Order { get; init; }
}
