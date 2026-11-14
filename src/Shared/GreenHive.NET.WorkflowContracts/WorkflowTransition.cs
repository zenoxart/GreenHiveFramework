namespace GreenHive.NET.WorkflowContracts;

/// <summary>Connects two steps. Evaluated in <see cref="Order"/>; the first matching condition wins; no condition = default.</summary>
public sealed record WorkflowTransition(string From, string To)
{
    /// <summary>Target that ends the workflow.</summary>
    public const string End = "end";

    /// <summary>WorkflowExpression over variables and the last step's result.</summary>
    public string? Condition { get; init; }

    public int Order { get; init; }
}
