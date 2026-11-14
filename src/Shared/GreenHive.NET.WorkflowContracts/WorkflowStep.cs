namespace GreenHive.NET.WorkflowContracts;

public enum WorkflowStepKind
{
    /// <summary>The user (or AI) fills a form described by an InputSchema.</summary>
    Input,

    /// <summary>Pick one of several options; the result drives the transition.</summary>
    Choice,

    /// <summary>Show collected data for confirmation before continuing.</summary>
    Review,

    /// <summary>Run a command of the central command manager (client side) or its headless endpoint.</summary>
    Command,

    /// <summary>A step type registered by a module (IWorkflowStepHandler / headless backend endpoint).</summary>
    ModuleStep,

    /// <summary>Pause until an event arrives, with timeout.</summary>
    WaitForEvent,

    /// <summary>A human must approve (always human, never AI).</summary>
    Approval,

    /// <summary>Free-form instruction for the AI agent with an explicit tool list and output schema.</summary>
    AiTask,

    /// <summary>Start another workflow and continue with its result.</summary>
    SubWorkflow,
}

/// <summary>One option of a Choice step.</summary>
public sealed record ChoiceOption(string Value, string Label);

/// <summary>One step of a workflow.</summary>
public sealed record WorkflowStep
{
    public required string Id { get; init; }

    public required WorkflowStepKind Kind { get; init; }

    /// <summary>TextKey or plain title.</summary>
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Input steps: the form definition (embedded) - its SchemaId/Version identify it in the module's contract surface.</summary>
    public InputSchema? Input { get; init; }

    /// <summary>Command steps: command ID of the central command manager.</summary>
    public string? CommandId { get; init; }

    /// <summary>ModuleStep steps: step type ID ("documents.approveInvoice") and version.</summary>
    public string? StepTypeId { get; init; }

    public int StepTypeVersion { get; init; } = 1;

    /// <summary>Parameter mapping: parameter name -> WorkflowExpression over variables.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    /// <summary>Output mapping: output name -> workflow variable.</summary>
    public IReadOnlyDictionary<string, string> OutputMapping { get; init; } = new Dictionary<string, string>();

    /// <summary>Choice steps: the options.</summary>
    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];

    /// <summary>WaitForEvent: event type and optional filter expression on the payload.</summary>
    public string? EventType { get; init; }

    public string? EventFilter { get; init; }

    /// <summary>AiTask: instruction and allowed tools (command IDs / step type IDs).</summary>
    public string? Instruction { get; init; }

    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>SubWorkflow: the workflow to start.</summary>
    public Guid? SubWorkflowId { get; init; }

    /// <summary>Approval: permission or role names of the approvers.</summary>
    public IReadOnlyList<string> Approvers { get; init; } = [];

    public bool RequiresHuman { get; init; }

    public bool AiAllowed { get; init; } = true;

    /// <summary>The AI needs a human approval before this step is executed.</summary>
    public bool Critical { get; init; }

    public string? CompensationStepId { get; init; }

    public int? TimeoutSeconds { get; init; }

    public int MaxRetries { get; init; } = 2;

    /// <summary>Steps that can only be performed by a person.</summary>
    public bool IsHumanOnly => RequiresHuman || Kind == WorkflowStepKind.Approval;

    /// <summary>Steps that wait for a person (or the AI) to provide something.</summary>
    public bool IsInteractive => Kind is WorkflowStepKind.Input or WorkflowStepKind.Choice or WorkflowStepKind.Review or WorkflowStepKind.Approval;
}
