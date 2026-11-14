namespace GreenHive.NET.CommandContracts;

/// <summary>Kind of a node in a generated command tree.</summary>
public enum CommandTreeNodeKind
{
    Group,
    Command,
    Separator,
}

/// <summary>
/// Immutable node of a generated command tree. It never references handlers or module types -
/// only IDs and display data - so the UI can keep it across hot-swaps.
/// </summary>
public sealed record CommandTreeNode(string Id, CommandTreeNodeKind Kind)
{
    public string? CommandId { get; init; }

    /// <summary>Base text key; the label is "&lt;TextKey&gt;.label".</summary>
    public string? TextKey { get; init; }

    public string? Icon { get; init; }

    public CommandKind CommandKind { get; init; }

    public IReadOnlyList<CommandTreeNode> Children { get; init; } = [];
}
