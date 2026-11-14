namespace GreenHive.NET.Monads;

/// <summary>
/// Applicative validation: runs ALL checks and collects every error (Result stops at the first one).
/// Use for incoming requests and smart constructors of value objects.
/// </summary>
public readonly struct Validation<T>
{
    private readonly T? _value;
    private readonly IReadOnlyList<Error>? _errors;

    private Validation(T value)
    {
        _value = value;
        _errors = null;
    }

    private Validation(IReadOnlyList<Error> errors)
    {
        _value = default;
        _errors = errors;
    }

    public bool IsValid => _errors is null || _errors.Count == 0;

    public IReadOnlyList<Error> Errors => _errors ?? [];

    public static Validation<T> Valid(T value) => new(value);

    public static Validation<T> Invalid(params Error[] errors) => new(errors);

    public static Validation<T> Invalid(IReadOnlyList<Error> errors) => new(errors);

    public static implicit operator Validation<T>(T value) => new(value);

    public static implicit operator Validation<T>(Error error) => new([error]);

    public Validation<TResult> Map<TResult>(Func<T, TResult> map) =>
        IsValid ? Validation<TResult>.Valid(map(_value!)) : Validation<TResult>.Invalid(Errors);

    public Validation<TResult> Bind<TResult>(Func<T, Validation<TResult>> bind) =>
        IsValid ? bind(_value!) : Validation<TResult>.Invalid(Errors);

    /// <summary>Adds a further rule on the value; errors of both are kept.</summary>
    public Validation<T> Ensure(Func<T, bool> predicate, Error error) =>
        IsValid ? predicate(_value!) ? this : Invalid(error) : this;

    public bool TryGetValue(out T value)
    {
        value = _value!;
        return IsValid;
    }

    public TResult Match<TResult>(Func<T, TResult> valid, Func<IReadOnlyList<Error>, TResult> invalid) =>
        IsValid ? valid(_value!) : invalid(Errors);

    /// <summary>Continues on the Result railway: Kind = Validation with all field errors.</summary>
    public Result<T> ToResult() => IsValid ? Result<T>.Success(_value!) : Result<T>.Failure(Error.Many(Errors));
}

public static class Validation
{
    public static Validation<T> Valid<T>(T value) => Validation<T>.Valid(value);

    public static Validation<T> Invalid<T>(params Error[] errors) => Validation<T>.Invalid(errors);

    public static Validation<(T1, T2)> Combine<T1, T2>(Validation<T1> v1, Validation<T2> v2) =>
        Errors(v1.Errors, v2.Errors) is { Count: > 0 } errors
            ? Validation<(T1, T2)>.Invalid(errors)
            : Validation<(T1, T2)>.Valid((Value(v1), Value(v2)));

    public static Validation<(T1, T2, T3)> Combine<T1, T2, T3>(Validation<T1> v1, Validation<T2> v2, Validation<T3> v3) =>
        Combine(v1, v2, v3, (a, b, c) => (a, b, c));

    public static Validation<TResult> Combine<T1, T2, TResult>(Validation<T1> v1, Validation<T2> v2, Func<T1, T2, TResult> map) =>
        Combine(v1, v2).Map(t => map(t.Item1, t.Item2));

    public static Validation<TResult> Combine<T1, T2, T3, TResult>(
        Validation<T1> v1, Validation<T2> v2, Validation<T3> v3, Func<T1, T2, T3, TResult> map) =>
        Errors(v1.Errors, v2.Errors, v3.Errors) is { Count: > 0 } errors
            ? Validation<TResult>.Invalid(errors)
            : Validation<TResult>.Valid(map(Value(v1), Value(v2), Value(v3)));

    public static Validation<TResult> Combine<T1, T2, T3, T4, TResult>(
        Validation<T1> v1, Validation<T2> v2, Validation<T3> v3, Validation<T4> v4, Func<T1, T2, T3, T4, TResult> map) =>
        Errors(v1.Errors, v2.Errors, v3.Errors, v4.Errors) is { Count: > 0 } errors
            ? Validation<TResult>.Invalid(errors)
            : Validation<TResult>.Valid(map(Value(v1), Value(v2), Value(v3), Value(v4)));

    public static Validation<TResult> Combine<T1, T2, T3, T4, T5, TResult>(
        Validation<T1> v1, Validation<T2> v2, Validation<T3> v3, Validation<T4> v4, Validation<T5> v5,
        Func<T1, T2, T3, T4, T5, TResult> map) =>
        Errors(v1.Errors, v2.Errors, v3.Errors, v4.Errors, v5.Errors) is { Count: > 0 } errors
            ? Validation<TResult>.Invalid(errors)
            : Validation<TResult>.Valid(map(Value(v1), Value(v2), Value(v3), Value(v4), Value(v5)));

    /// <summary>Validates a value against several independent rules and keeps all failures.</summary>
    public static Validation<T> All<T>(T value, params Func<T, Validation<T>>[] rules)
    {
        var errors = rules.SelectMany(rule => rule(value).Errors).ToList();
        return errors.Count == 0 ? Validation<T>.Valid(value) : Validation<T>.Invalid(errors);
    }

    // ---- common field rules (messages are TextKeys of the "validation" scope) ----

    public static Validation<string> Required(string field, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Error.Field(field, $"validation.{field}.required", "validation:required")
            : Validation<string>.Valid(value.Trim());

    public static Validation<string?> MaxLength(string field, string? value, int max) =>
        value is not null && value.Length > max
            ? Error.Field(field, $"validation.{field}.maxLength", $"validation:maxLength|{max}")
            : Validation<string?>.Valid(value);

    public static Validation<string> RequiredMax(string field, string? value, int max) =>
        Required(field, value).Bind(v => v.Length > max
            ? Error.Field(field, $"validation.{field}.maxLength", $"validation:maxLength|{max}")
            : Validation<string>.Valid(v));

    public static Validation<T> Range<T>(string field, T value, T min, T max)
        where T : IComparable<T> =>
        value.CompareTo(min) < 0 || value.CompareTo(max) > 0
            ? Error.Field(field, $"validation.{field}.range", $"validation:range|{min}|{max}")
            : Validation<T>.Valid(value);

    public static Validation<Guid> NotEmpty(string field, Guid value) =>
        value == Guid.Empty ? Error.Field(field, $"validation.{field}.required", "validation:required") : Validation<Guid>.Valid(value);

    public static Validation<string> Email(string field, string? value) =>
        Required(field, value).Bind(v =>
            v.IndexOf('@') is > 0 and var at && at < v.Length - 3 && v.IndexOf('.', at) > at + 1 && !v.Contains(' ')
                ? Validation<string>.Valid(v.ToLowerInvariant())
                : Error.Field(field, $"validation.{field}.email", "validation:email"));

    public static Validation<string> OneOf(string field, string? value, params string[] allowed) =>
        value is not null && allowed.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? Validation<string>.Valid(allowed.First(a => a.Equals(value, StringComparison.OrdinalIgnoreCase)))
            : Error.Field(field, $"validation.{field}.oneOf", $"validation:oneOf|{string.Join(", ", allowed)}");

    private static T Value<T>(Validation<T> validation) => validation.Match(v => v, _ => default!);

    private static IReadOnlyList<Error> Errors(params IReadOnlyList<Error>[] lists) => lists.SelectMany(l => l).ToList();
}
