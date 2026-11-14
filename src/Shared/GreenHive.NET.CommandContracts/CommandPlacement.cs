namespace GreenHive.NET.CommandContracts;

/// <summary>
/// Where a command or group appears in a command tree.
/// Placement = tree name (shell.mainmenu, shell.navigation, shell.wheel, context.*, panel.*);
/// Path = parent group path inside the tree ("file/export"); Section = block separated by
/// separators; Order = position inside the section.
/// </summary>
public sealed record CommandPlacement(string Placement, string Path = "", int Section = 0, int Order = 0);
