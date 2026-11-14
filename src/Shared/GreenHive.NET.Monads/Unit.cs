namespace GreenHive.NET.Monads;

/// <summary>The "no value" type, so operations without a result still return <c>Result&lt;Unit&gt;</c>.</summary>
public readonly record struct Unit
{
    /// <summary>The only value of <see cref="Unit"/>.</summary>
    public static readonly Unit Value = default;

    public override string ToString() => "()";
}
