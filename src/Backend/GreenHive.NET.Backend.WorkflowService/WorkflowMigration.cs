using System.Text.RegularExpressions;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Backend.WorkflowService;

/// <summary>Result of classifying one workflow against a module update.</summary>
public sealed record MigrationResult(ImpactSeverity Severity, IReadOnlyList<ImpactReason> Reasons, WorkflowDefinition? Migrated);

/// <summary>
/// Migrations of workflow definitions between module versions: declared renames (manifest migrations: command and step
/// type replacements, field renames) and generic rules (new optional fields, compatible schema changes).
/// Migrations are data transformations - the definition is rewritten, never code.
/// </summary>
public static class WorkflowMigration
{
    public static MigrationResult Analyze(WorkflowDefinition definition, IReadOnlyList<SurfaceChange> diff, ModuleContractSurface newSurface)
    {
        var reasons = new List<ImpactReason>();
        var severity = ImpactSeverity.Unaffected;
        var steps = definition.Steps.ToList();
        var triggers = definition.Triggers.ToList();
        var renamedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var migrated = false;

        void Raise(ImpactSeverity s, string stepId, DependencyKind kind, string id, string reason)
        {
            severity = (ImpactSeverity)Math.Max((int)severity, (int)s);
            reasons.Add(new ImpactReason(stepId, kind, id, reason));
        }

        SurfaceChange? Change(DependencyKind kind, string? id) =>
            id is null ? null : diff.FirstOrDefault(c => c.Kind == kind && c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            if (Change(DependencyKind.Command, step.CommandId) is { } command)
            {
                switch (command.Change)
                {
                    case SurfaceChangeKind.Renamed when command.ReplacedBy is { } to:
                        steps[i] = step = step with { CommandId = to };
                        migrated = true;
                        Raise(ImpactSeverity.AutoMigrated, step.Id, DependencyKind.Command, command.Id, $"Command renamed to '{to}' - migrated automatically.");
                        break;
                    case SurfaceChangeKind.Removed:
                        Raise(ImpactSeverity.Broken, step.Id, DependencyKind.Command, command.Id, "The command was removed from the module.");
                        break;
                    case SurfaceChangeKind.ChangedBreaking:
                        Raise(ImpactSeverity.Broken, step.Id, DependencyKind.Command, command.Id, command.Detail);
                        break;
                }
            }

            if (Change(DependencyKind.StepType, step.StepTypeId) is { } stepType)
            {
                switch (stepType.Change)
                {
                    case SurfaceChangeKind.Renamed when stepType.ReplacedBy is { } to:
                        steps[i] = step = step with { StepTypeId = to };
                        migrated = true;
                        Raise(ImpactSeverity.AutoMigrated, step.Id, DependencyKind.StepType, stepType.Id, $"Step type renamed to '{to}' - migrated automatically.");
                        break;
                    case SurfaceChangeKind.Removed:
                        Raise(ImpactSeverity.Broken, step.Id, DependencyKind.StepType, stepType.Id, "The step type was removed from the module.");
                        break;
                    case SurfaceChangeKind.ChangedBreaking:
                        Raise(ImpactSeverity.ReviewRequired, step.Id, DependencyKind.StepType, stepType.Id, stepType.Detail);
                        break;
                    case SurfaceChangeKind.ChangedCompatible:
                        Raise(ImpactSeverity.Compatible, step.Id, DependencyKind.StepType, stepType.Id, stepType.Detail);
                        break;
                }
            }

            if (step.Input is { } schema && Change(DependencyKind.InputSchema, schema.SchemaId) is { } schemaChange)
            {
                var newSchema = newSurface.InputSchemas.FirstOrDefault(s => s.SchemaId.Equals(schema.SchemaId, StringComparison.OrdinalIgnoreCase));
                if (schemaChange.Change == SurfaceChangeKind.Removed || newSchema is null)
                {
                    Raise(ImpactSeverity.Broken, step.Id, DependencyKind.InputSchema, schema.SchemaId, "The input schema was removed.");
                }
                else
                {
                    var renames = newSurface.Migrations
                        .Where(m => m.Kind == DependencyKind.InputSchema && m.From.Equals(schema.SchemaId, StringComparison.OrdinalIgnoreCase))
                        .SelectMany(m => m.FieldRenames).ToList();
                    foreach (var (from, to) in renames)
                    {
                        renamedFields[from] = to;
                    }

                    steps[i] = step = step with { Input = newSchema };
                    migrated = true;
                    var breaking = schemaChange.Change == SurfaceChangeKind.ChangedBreaking;
                    Raise(breaking ? ImpactSeverity.ReviewRequired : ImpactSeverity.AutoMigrated, step.Id, DependencyKind.InputSchema, schema.SchemaId,
                        breaking
                            ? $"{schemaChange.Detail} - the form was updated; check that people and the AI can provide the new values."
                            : $"{schemaChange.Detail} - migrated automatically.");
                }
            }

            if (step.Kind == WorkflowStepKind.WaitForEvent && Change(DependencyKind.Event, step.EventType) is { } waitEvent)
            {
                Raise(waitEvent.IsBreaking ? ImpactSeverity.Broken : ImpactSeverity.Compatible, step.Id, DependencyKind.Event, waitEvent.Id, waitEvent.Detail);
            }
        }

        foreach (var trigger in triggers)
        {
            if (Change(DependencyKind.Event, trigger.EventType) is { } change && change.IsBreaking)
            {
                Raise(change.Change == SurfaceChangeKind.Removed ? ImpactSeverity.Broken : ImpactSeverity.ReviewRequired,
                    $"trigger:{trigger.Id}", DependencyKind.Event, change.Id, change.Detail);
            }

            if (Change(DependencyKind.Command, trigger.CommandId) is { } commandChange)
            {
                Raise(commandChange.Change == SurfaceChangeKind.Renamed ? ImpactSeverity.AutoMigrated : ImpactSeverity.Broken,
                    $"trigger:{trigger.Id}", DependencyKind.Command, commandChange.Id, commandChange.Detail);
            }
        }

        if (severity == ImpactSeverity.Unaffected)
        {
            return new MigrationResult(ImpactSeverity.Compatible, reasons, null);
        }

        if (!migrated)
        {
            return new MigrationResult(severity, reasons, null);
        }

        // Rename variables in expressions and mappings (vars.note -> vars.comment, steps.x.note -> steps.x.comment).
        steps = steps.Select(s => s with
        {
            Parameters = s.Parameters.ToDictionary(kv => kv.Key, kv => RenameFields(kv.Value, renamedFields)),
            OutputMapping = s.OutputMapping.ToDictionary(kv => renamedFields.GetValueOrDefault(kv.Key, kv.Key), kv => kv.Value),
            EventFilter = s.EventFilter is null ? null : RenameFields(s.EventFilter, renamedFields),
        }).ToList();
        triggers = triggers.Select(t => t with
        {
            CommandId = t.CommandId is { } id && Change(DependencyKind.Command, id)?.ReplacedBy is { } to ? to : t.CommandId,
            Filter = t.Filter is null ? null : RenameFields(t.Filter, renamedFields),
        }).ToList();
        var transitions = definition.Transitions.Select(t => t with { Condition = t.Condition is null ? null : RenameFields(t.Condition, renamedFields) }).ToList();

        return new MigrationResult(severity, reasons, definition with { Steps = steps, Triggers = triggers, Transitions = transitions });
    }

    private static string RenameFields(string expression, IReadOnlyDictionary<string, string> renames)
    {
        foreach (var (from, to) in renames)
        {
            expression = Regex.Replace(expression, $@"\b((?:vars|last|steps\.[A-Za-z0-9_\-]+)\.){Regex.Escape(from)}\b", $"$1{to}", RegexOptions.IgnoreCase);
        }

        return expression;
    }
}
