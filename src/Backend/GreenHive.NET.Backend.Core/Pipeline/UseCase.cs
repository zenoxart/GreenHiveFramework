using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.Core.Pipeline;

/// <summary>
/// The shape of every backend operation in the monad architecture (railway; each arrow = Bind):
/// request -> Validate -> Authorize -> Load -> Decide (pure) -> Persist (transaction) -> Publish (outbox) -> Project.
/// One use case per class, named after the action. Workflow steps, AI tool calls and HTTP endpoints all call the same
/// use case, so humans, the workflow engine and the AI agent get identical behaviour and errors.
/// </summary>
public interface IUseCase<in TRequest, TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
