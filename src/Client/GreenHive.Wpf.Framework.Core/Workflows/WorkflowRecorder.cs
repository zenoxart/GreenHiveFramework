using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>
/// Records the workflow-capable commands a user executes inside the app and turns them into a draft workflow
/// (one Command step per interaction, in order) that can be refined in the designer and published.
/// </summary>
public sealed class WorkflowRecorder : AppObject
{
    private readonly List<(string CommandId, string? Parameter)> _steps = [];

    public event EventHandler? Changed;

    public bool IsRecording { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int StepCount => _steps.Count;

    public void Start(string name)
    {
        _steps.Clear();
        Name = name;
        IsRecording = true;
        AppContext.Log.Write($"Recording workflow '{name}'", LogEntryType.Information, nameof(WorkflowRecorder));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Record(string commandId, object? parameter)
    {
        if (!IsRecording)
        {
            return;
        }

        _steps.Add((commandId, parameter?.ToString()));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops recording and returns the draft (null when nothing was recorded).</summary>
    public WorkflowDefinition? Stop()
    {
        IsRecording = false;
        Changed?.Invoke(this, EventArgs.Empty);
        if (_steps.Count == 0)
        {
            return null;
        }

        var steps = _steps.Select((s, i) => new WorkflowStep
        {
            Id = $"step{i + 1}",
            Kind = WorkflowStepKind.Command,
            Title = s.CommandId,
            CommandId = s.CommandId,
            Parameters = s.Parameter is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["parameter"] = $"'{s.Parameter.Replace("'", "\\'")}'" },
        }).ToList();
        var transitions = steps.Select((s, i) => new WorkflowTransition(s.Id, i + 1 < steps.Count ? steps[i + 1].Id : WorkflowTransition.End)).ToList();
        return new WorkflowDefinition
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "Recorded workflow" : Name,
            Description = "Recorded in the app.",
            Origin = WorkflowOrigin.User,
            StartStepId = steps[0].Id,
            Steps = steps,
            Transitions = transitions,
            Triggers = new WorkflowTrigger[] { new WorkflowTrigger { Kind = WorkflowTriggerKind.Manual } },
        };
    }
}
