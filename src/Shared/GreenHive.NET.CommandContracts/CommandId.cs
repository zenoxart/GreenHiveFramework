namespace GreenHive.NET.CommandContracts;

/// <summary>
/// Stable command ID in the format &lt;prefix&gt;.&lt;action&gt; (e.g. documents.create). Stable across module versions,
/// so bindings, shortcuts and menus remain valid after a hot-swap.
/// </summary>
public static class CommandId
{
    public static string Create(string prefix, params string[] parts) =>
        string.Join('.', new[] { prefix }.Concat(parts).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));

    /// <summary>The prefix (module or shell) of a command ID.</summary>
    public static string PrefixOf(string commandId)
    {
        var dot = commandId.IndexOf('.');
        return dot <= 0 ? commandId : commandId[..dot];
    }

    public static bool IsValid(string? commandId) =>
        !string.IsNullOrWhiteSpace(commandId) && commandId.IndexOf('.') > 0 && !commandId.EndsWith('.') &&
        commandId.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_');
}
