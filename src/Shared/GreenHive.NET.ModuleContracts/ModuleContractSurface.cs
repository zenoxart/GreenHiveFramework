using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.ModuleContracts;

public sealed record CommandSurface(string Id, int ParameterSchemaVersion = 1)
{
    public bool WorkflowCapable { get; init; }

    public bool AiAllowed { get; init; }

    /// <summary>Headless endpoint relative to the gateway, if the command can run server-side.</summary>
    public string? Endpoint { get; init; }
}

public sealed record EventSurface(string Type, int PayloadVersion = 1);

/// <summary>A declared rename or replacement to the previous version (used by WorkflowMigration).</summary>
public sealed record SurfaceMigration(DependencyKind Kind, string From, string To)
{
    /// <summary>Field renames inside an input schema / step type: old name -> new name.</summary>
    public IReadOnlyDictionary<string, string> FieldRenames { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Everything a module version offers to workflows and to other parts of the system. Published with every module
/// version; the Module Registry diffs it between versions and the WorkflowService uses the diff for its impact analysis.
/// </summary>
public sealed record ModuleContractSurface
{
    public IReadOnlyList<CommandSurface> Commands { get; init; } = [];

    public IReadOnlyList<InputSchema> InputSchemas { get; init; } = [];

    public IReadOnlyList<StepTypeDescriptor> StepTypes { get; init; } = [];

    public IReadOnlyList<EventSurface> Events { get; init; } = [];

    public string BackendApiVersion { get; init; } = "v1";

    public IReadOnlyList<WorkflowDefinition> Templates { get; init; } = [];

    public IReadOnlyList<SurfaceMigration> Migrations { get; init; } = [];

    /// <summary>True if the surface declares the ID (command, schema, step type or event).</summary>
    public bool Declares(DependencyKind kind, string id) => kind switch
    {
        DependencyKind.Command => Commands.Any(c => Same(c.Id, id)),
        DependencyKind.InputSchema => InputSchemas.Any(s => Same(s.SchemaId, id)),
        DependencyKind.StepType => StepTypes.Any(s => Same(s.Id, id)),
        DependencyKind.Event => Events.Any(e => Same(e.Type, id)),
        _ => false,
    };

    /// <summary>Compares two versions of a module's surface and classifies every difference.</summary>
    public static IReadOnlyList<SurfaceChange> Diff(ModuleContractSurface oldSurface, ModuleContractSurface newSurface)
    {
        var changes = new List<SurfaceChange>();
        var migrations = newSurface.Migrations;

        string? RenamedTo(DependencyKind kind, string id) =>
            migrations.FirstOrDefault(m => m.Kind == kind && Same(m.From, id))?.To;

        // Commands
        foreach (var old in oldSurface.Commands)
        {
            var current = newSurface.Commands.FirstOrDefault(c => Same(c.Id, old.Id));
            if (current is null)
            {
                changes.Add(RenamedTo(DependencyKind.Command, old.Id) is { } to
                    ? new SurfaceChange(DependencyKind.Command, old.Id, SurfaceChangeKind.Renamed, $"Command renamed to '{to}'.") { ReplacedBy = to }
                    : new SurfaceChange(DependencyKind.Command, old.Id, SurfaceChangeKind.Removed, "Command was removed."));
            }
            else if (current.ParameterSchemaVersion != old.ParameterSchemaVersion)
            {
                changes.Add(new SurfaceChange(DependencyKind.Command, old.Id, SurfaceChangeKind.ChangedBreaking,
                    $"Parameter schema changed from v{old.ParameterSchemaVersion} to v{current.ParameterSchemaVersion}."));
            }
            else if (old.AiAllowed && !current.AiAllowed)
            {
                changes.Add(new SurfaceChange(DependencyKind.Command, old.Id, SurfaceChangeKind.ChangedBreaking, "Command is no longer allowed for AI."));
            }
        }

        changes.AddRange(newSurface.Commands.Where(c => !oldSurface.Commands.Any(o => Same(o.Id, c.Id)))
            .Select(c => new SurfaceChange(DependencyKind.Command, c.Id, SurfaceChangeKind.Added, "New command.")));

        // Input schemas
        foreach (var old in oldSurface.InputSchemas)
        {
            var current = newSurface.InputSchemas.FirstOrDefault(s => Same(s.SchemaId, old.SchemaId));
            if (current is null)
            {
                changes.Add(new SurfaceChange(DependencyKind.InputSchema, old.SchemaId, SurfaceChangeKind.Removed, "Input schema was removed."));
                continue;
            }

            var schemaChange = CompareSchemas(old, current, migrations);
            if (schemaChange is not null)
            {
                changes.Add(schemaChange);
            }
        }

        changes.AddRange(newSurface.InputSchemas.Where(s => !oldSurface.InputSchemas.Any(o => Same(o.SchemaId, s.SchemaId)))
            .Select(s => new SurfaceChange(DependencyKind.InputSchema, s.SchemaId, SurfaceChangeKind.Added, "New input schema.")));

        // Step types
        foreach (var old in oldSurface.StepTypes)
        {
            var current = newSurface.StepTypes.FirstOrDefault(s => Same(s.Id, old.Id));
            if (current is null)
            {
                changes.Add(RenamedTo(DependencyKind.StepType, old.Id) is { } to
                    ? new SurfaceChange(DependencyKind.StepType, old.Id, SurfaceChangeKind.Renamed, $"Step type renamed to '{to}'.") { ReplacedBy = to }
                    : new SurfaceChange(DependencyKind.StepType, old.Id, SurfaceChangeKind.Removed, "Step type was removed."));
            }
            else if (current.Version > old.Version)
            {
                var breaking = current.Version / 100 != old.Version / 100 || (old.Headless && !current.Headless);
                changes.Add(new SurfaceChange(DependencyKind.StepType, old.Id,
                    breaking ? SurfaceChangeKind.ChangedBreaking : SurfaceChangeKind.ChangedCompatible,
                    $"Step type version {old.Version} -> {current.Version}."));
            }
        }

        changes.AddRange(newSurface.StepTypes.Where(s => !oldSurface.StepTypes.Any(o => Same(o.Id, s.Id)))
            .Select(s => new SurfaceChange(DependencyKind.StepType, s.Id, SurfaceChangeKind.Added, "New step type.")));

        // Events
        foreach (var old in oldSurface.Events)
        {
            var current = newSurface.Events.FirstOrDefault(e => Same(e.Type, old.Type));
            if (current is null)
            {
                changes.Add(new SurfaceChange(DependencyKind.Event, old.Type, SurfaceChangeKind.Removed, "Event is no longer published."));
            }
            else if (current.PayloadVersion != old.PayloadVersion)
            {
                changes.Add(new SurfaceChange(DependencyKind.Event, old.Type, SurfaceChangeKind.ChangedBreaking,
                    $"Payload changed from v{old.PayloadVersion} to v{current.PayloadVersion}."));
            }
        }

        changes.AddRange(newSurface.Events.Where(e => !oldSurface.Events.Any(o => Same(o.Type, e.Type)))
            .Select(e => new SurfaceChange(DependencyKind.Event, e.Type, SurfaceChangeKind.Added, "New event.")));

        if (!Same(oldSurface.BackendApiVersion, newSurface.BackendApiVersion))
        {
            changes.Add(new SurfaceChange(DependencyKind.BackendApi, oldSurface.BackendApiVersion, SurfaceChangeKind.ChangedBreaking,
                $"Backend API {oldSurface.BackendApiVersion} -> {newSurface.BackendApiVersion}."));
        }

        return changes;
    }

    private static SurfaceChange? CompareSchemas(InputSchema old, InputSchema current, IReadOnlyList<SurfaceMigration> migrations)
    {
        var renames = migrations.FirstOrDefault(m => m.Kind == DependencyKind.InputSchema && Same(m.From, old.SchemaId))?.FieldRenames
            ?? new Dictionary<string, string>();
        var breaking = new List<string>();
        var compatible = new List<string>();

        foreach (var field in old.Fields)
        {
            var name = renames.TryGetValue(field.Name, out var renamed) ? renamed : field.Name;
            var now = current.Find(name);
            if (now is null)
            {
                breaking.Add($"field '{field.Name}' removed");
            }
            else if (now.Type != field.Type)
            {
                breaking.Add($"field '{field.Name}' type {field.Type} -> {now.Type}");
            }
            else if (now.Required && !field.Required)
            {
                (now.Default is null ? breaking : compatible).Add($"field '{field.Name}' is now required");
            }
            else if (!Same(name, field.Name))
            {
                compatible.Add($"field '{field.Name}' renamed to '{name}'");
            }
        }

        foreach (var field in current.Fields.Where(f => !old.Fields.Any(o => Same(o.Name, f.Name) || (renames.TryGetValue(o.Name, out var r) && Same(r, f.Name)))))
        {
            (field.Required && field.Default is null ? breaking : compatible).Add(
                $"new {(field.Required ? "required" : "optional")} field '{field.Name}'");
        }

        if (breaking.Count > 0)
        {
            return new SurfaceChange(DependencyKind.InputSchema, old.SchemaId, SurfaceChangeKind.ChangedBreaking, string.Join("; ", breaking.Concat(compatible)));
        }

        return compatible.Count > 0
            ? new SurfaceChange(DependencyKind.InputSchema, old.SchemaId, SurfaceChangeKind.ChangedCompatible, string.Join("; ", compatible))
            : null;
    }

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
