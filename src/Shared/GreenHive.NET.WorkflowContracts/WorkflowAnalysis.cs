using GreenHive.NET.Monads;

namespace GreenHive.NET.WorkflowContracts;

/// <summary>
/// Checks a workflow definition and computes its dependencies. Used by the designer (client) and the
/// WorkflowService (on save), so both report the same problems.
/// </summary>
public static class WorkflowAnalysis
{
    /// <summary>Validates structure and expressions; collects all problems.</summary>
    public static Validation<WorkflowDefinition> Validate(WorkflowDefinition definition)
    {
        var errors = new List<Error>();
        void Fail(string field, string code, string message) => errors.Add(Error.Field(field, code, message));

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            Fail("name", "workflow.name.required", "workflow:validation.nameRequired");
        }

        if (definition.Steps.Count == 0)
        {
            Fail("steps", "workflow.steps.empty", "workflow:validation.noSteps");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in definition.Steps)
        {
            if (!ids.Add(step.Id))
            {
                Fail($"steps.{step.Id}", "workflow.step.duplicate", $"Duplicate step ID '{step.Id}'.");
            }

            switch (step.Kind)
            {
                case WorkflowStepKind.Input when step.Input is null:
                    Fail($"steps.{step.Id}", "workflow.step.inputSchema", $"Input step '{step.Id}' needs an input schema.");
                    break;
                case WorkflowStepKind.Command when string.IsNullOrWhiteSpace(step.CommandId):
                    Fail($"steps.{step.Id}", "workflow.step.command", $"Command step '{step.Id}' needs a command ID.");
                    break;
                case WorkflowStepKind.ModuleStep when string.IsNullOrWhiteSpace(step.StepTypeId):
                    Fail($"steps.{step.Id}", "workflow.step.stepType", $"Module step '{step.Id}' needs a step type.");
                    break;
                case WorkflowStepKind.WaitForEvent when string.IsNullOrWhiteSpace(step.EventType):
                    Fail($"steps.{step.Id}", "workflow.step.event", $"Wait step '{step.Id}' needs an event type.");
                    break;
                case WorkflowStepKind.Choice when step.Options.Count == 0:
                    Fail($"steps.{step.Id}", "workflow.step.options", $"Choice step '{step.Id}' needs options.");
                    break;
            }

            foreach (var (name, expression) in step.Parameters)
            {
                CheckExpression($"steps.{step.Id}.parameters.{name}", expression, errors);
            }

            if (step.EventFilter is not null)
            {
                CheckExpression($"steps.{step.Id}.eventFilter", step.EventFilter, errors);
            }
        }

        if (!ids.Contains(definition.StartStepId))
        {
            Fail("startStepId", "workflow.start.unknown", $"Start step '{definition.StartStepId}' does not exist.");
        }

        foreach (var transition in definition.Transitions)
        {
            if (!ids.Contains(transition.From))
            {
                Fail("transitions", "workflow.transition.from", $"Transition from unknown step '{transition.From}'.");
            }

            if (!transition.To.Equals(WorkflowTransition.End, StringComparison.OrdinalIgnoreCase) && !ids.Contains(transition.To))
            {
                Fail("transitions", "workflow.transition.to", $"Transition to unknown step '{transition.To}'.");
            }

            if (transition.Condition is not null)
            {
                CheckExpression($"transitions.{transition.From}->{transition.To}", transition.Condition, errors);
            }
        }

        foreach (var trigger in definition.Triggers)
        {
            if (trigger.Kind == WorkflowTriggerKind.Event && string.IsNullOrWhiteSpace(trigger.EventType))
            {
                Fail($"triggers.{trigger.Id}", "workflow.trigger.event", "Event triggers need an event type.");
            }

            if (trigger.Filter is not null)
            {
                CheckExpression($"triggers.{trigger.Id}.filter", trigger.Filter, errors);
            }

            foreach (var (variable, expression) in trigger.PayloadMapping)
            {
                CheckExpression($"triggers.{trigger.Id}.mapping.{variable}", expression, errors);
            }
        }

        return errors.Count == 0 ? Validation<WorkflowDefinition>.Valid(definition) : Validation<WorkflowDefinition>.Invalid(errors);
    }

    /// <summary>
    /// Computes the dependencies of a definition.
    /// <paramref name="moduleOf"/> maps a command / step type / event / schema ID to its module ID (null = shell);
    /// <paramref name="moduleVersion"/> returns the module's current version.
    /// </summary>
    public static IReadOnlyList<WorkflowDependency> ComputeDependencies(
        WorkflowDefinition definition, Func<string, string?> moduleOf, Func<string, string?> moduleVersion)
    {
        var map = new Dictionary<(DependencyKind, string), (string Version, HashSet<string> Steps)>();
        void Add(DependencyKind kind, string id, string version, string stepId)
        {
            if (!map.TryGetValue((kind, id), out var entry))
            {
                entry = (version, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                map[(kind, id)] = entry;
            }

            entry.Steps.Add(stepId);
            if (moduleOf(id) is { } module && kind != DependencyKind.Module)
            {
                var v = moduleVersion(module) ?? "1.0.0";
                Add(DependencyKind.Module, module, SemanticVersionRange(v), stepId);
            }
        }

        foreach (var step in definition.Steps)
        {
            if (step.Input is { } schema)
            {
                Add(DependencyKind.InputSchema, schema.SchemaId, schema.Version.ToString(), step.Id);
            }

            if (step.CommandId is { } command)
            {
                Add(DependencyKind.Command, command, "1", step.Id);
            }

            if (step.StepTypeId is { } stepType)
            {
                Add(DependencyKind.StepType, stepType, step.StepTypeVersion.ToString(), step.Id);
            }

            if (step.EventType is { } eventType)
            {
                Add(DependencyKind.Event, eventType, "1", step.Id);
            }
        }

        foreach (var trigger in definition.Triggers)
        {
            if (trigger.EventType is { } eventType)
            {
                Add(DependencyKind.Event, eventType, "1", $"trigger:{trigger.Id}");
            }

            if (trigger.CommandId is { } command)
            {
                Add(DependencyKind.Command, command, "1", $"trigger:{trigger.Id}");
            }
        }

        return map.Select(kv => new WorkflowDependency(kv.Key.Item1, kv.Key.Item2, kv.Value.Version) { UsedBySteps = kv.Value.Steps.Order().ToList() })
            .OrderBy(d => d.Kind).ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SemanticVersionRange(string version)
    {
        var major = version.Split('.')[0];
        return $"^{(int.TryParse(major, out _) ? version : "1.0.0")}";
    }

    private static void CheckExpression(string field, string expression, List<Error> errors)
    {
        var parsed = WorkflowExpression.Parse(expression);
        if (parsed.IsFailure)
        {
            errors.Add(Error.Field(field, parsed.Error.Code, parsed.Error.Message));
        }
    }
}
