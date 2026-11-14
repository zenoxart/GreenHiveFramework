namespace GreenHive.NET.CommandContracts;

/// <summary>Kind of a command.</summary>
public enum CommandKind
{
    Normal,
    Toggle,
}

/// <summary>Metadata of a command, as registered with the central command manager.</summary>
public sealed record CommandDescriptor(string Id)
{
    /// <summary>
    /// Base text key, e.g. "documents:invoice.approve". Texts follow by convention:
    /// .label, .tooltip, .description, .disabled.&lt;reason&gt;.
    /// </summary>
    public string? TextKey { get; init; }

    /// <summary>Icon reference (IconRef), e.g. "save" or "documents.invoice".</summary>
    public string? Icon { get; init; }

    /// <summary>Permission required to see and execute the command (UI gating only).</summary>
    public string? Permission { get; init; }

    public CommandKind Kind { get; init; } = CommandKind.Normal;

    public IReadOnlyList<CommandPlacement> Placements { get; init; } = [];

    /// <summary>Default shortcuts; the ShortcutManager decides the effective bindings.</summary>
    public IReadOnlyList<ShortcutBinding> DefaultShortcuts { get; init; } = [];

    /// <summary>May be used as a workflow step and is recorded by the workflow recorder.</summary>
    public bool WorkflowCapable { get; init; }

    /// <summary>May be offered to the AI agent as a tool.</summary>
    public bool AiAllowed { get; init; }
}
