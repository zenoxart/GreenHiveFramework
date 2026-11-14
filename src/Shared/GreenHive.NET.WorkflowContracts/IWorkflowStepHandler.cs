using System.Text.Json.Nodes;
using GreenHive.NET.Monads;

namespace GreenHive.NET.WorkflowContracts;

/// <summary>Description of a module step type.</summary>
public sealed record StepTypeDescriptor(string Id, int Version, string Title)
{
    public InputSchema? Input { get; init; }

    /// <summary>Names of the outputs the step produces.</summary>
    public IReadOnlyList<string> Outputs { get; init; } = [];

    /// <summary>Can run without UI (required for AI and server-side execution).</summary>
    public bool Headless { get; init; } = true;

    /// <summary>Headless endpoint relative to the gateway (POST, body = inputs, response = outputs).</summary>
    public string? Endpoint { get; init; }
}

/// <summary>Context of a step execution.</summary>
public sealed record WorkflowStepContext(Guid InstanceId, Guid WorkflowId, string StepId, Guid RunAs)
{
    public JsonObject Variables { get; init; } = [];

    /// <summary>Actor kind of the execution (human in the app, engine on the server, AI agent).</summary>
    public ActorKind Actor { get; init; } = ActorKind.Human;
}

/// <summary>
/// Contract a module implements to offer its own workflow step types. Headless execution must call the module
/// backend, never the module's UI, so the same step works for a user in the app, the server engine and the AI agent.
/// </summary>
public interface IWorkflowStepHandler
{
    StepTypeDescriptor Describe();

    Task<Validation<JsonObject>> ValidateAsync(JsonObject inputs, CancellationToken cancellationToken);

    Task<Result<JsonObject>> ExecuteAsync(JsonObject inputs, WorkflowStepContext context, CancellationToken cancellationToken);
}
