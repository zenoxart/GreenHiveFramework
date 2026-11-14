using System.Text.Json.Nodes;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;
using GreenHive.NET.WorkflowContracts;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.Wpf.Framework.Workflows;

/// <summary>
/// Executes steps that need the client: commands without a headless endpoint (run through the central command manager)
/// and module step types with a client handler. The result completes the step on the server.
/// </summary>
public sealed class WorkflowRunner : AppObject
{
    private WorkflowManager Workflows => Context<IWorkflowContext>().Workflows;

    public bool CanRun(InboxItem item, WorkflowStep? step) =>
        item.StepKind == WorkflowStepKind.Command && step?.CommandId is { } command && Context<ICommandContext>().Commands.GetDescriptor(command) is not null
        || item.StepKind == WorkflowStepKind.ModuleStep && step?.StepTypeId is { } type && Workflows.StepTypes.ContainsKey(type);

    public async Task<Result<WorkflowInstance>> RunAsync(InboxItem item, WorkflowStep step, CancellationToken ct = default)
    {
        var parameters = Evaluate(step, item.Variables);
        if (step.Kind == WorkflowStepKind.Command && step.CommandId is { } commandId)
        {
            var parameter = parameters.Count == 1 ? parameters.First().Value?.ToString() : parameters.ToJsonString();
            var executed = await Context<ICommandContext>().Commands.ExecuteAsync(commandId, parameter);
            return executed
                ? await Workflows.CompleteAsync(item.InstanceId, new CompleteStepRequest(step.Id) { Outputs = new JsonObject { ["executed"] = true } }, ct)
                : Error.Conflict("workflow.command.notExecuted", "workflow:command.notExecuted");
        }

        if (step.StepTypeId is { } typeId && Workflows.StepTypes.TryGetValue(typeId, out var registration))
        {
            var context = new WorkflowStepContext(item.InstanceId, item.WorkflowId, step.Id, Context<ISecurityContext>().Backend.CurrentUser?.Id ?? Guid.Empty)
            {
                Variables = item.Variables,
            };
            return await registration.Handler.ValidateAsync(parameters, ct).ToResult()
                .BindAsync(valid => registration.Handler.ExecuteAsync(valid, context, ct))
                .BindAsync(outputs => Workflows.CompleteAsync(item.InstanceId, new CompleteStepRequest(step.Id) { Outputs = outputs }, ct));
        }

        return Error.NotFound("workflow.step.noClientHandler", "workflow:step.noClientHandler");
    }

    private static JsonObject Evaluate(WorkflowStep step, JsonObject variables)
    {
        var scope = WorkflowExpression.Scope(variables);
        var result = new JsonObject();
        foreach (var (name, expression) in step.Parameters)
        {
            result[name] = WorkflowExpression.ToNode(WorkflowExpression.Evaluate(expression, scope).GetValueOrDefault(null));
        }

        return result;
    }
}
