namespace GreenHive.NET.CommandContracts;

/// <summary>A dynamic child of a group: a command plus its parameter.</summary>
public sealed record DynamicCommandItem(string CommandId, string Label, object? Parameter = null)
{
    public string? Icon { get; init; }
}

/// <summary>
/// Supplies dynamic children for a group at the moment it opens (recent documents, open windows, report templates).
/// Must be fast; long work is cancelled when the menu closes. Removed automatically when the owning module unloads.
/// </summary>
public interface ICommandTreeProvider
{
    /// <summary>Placement and group path this provider fills.</summary>
    string Placement { get; }

    string Path { get; }

    Task<IReadOnlyList<DynamicCommandItem>> GetChildrenAsync(CancellationToken cancellationToken);
}
