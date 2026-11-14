namespace GreenHive.NET.Monads;

/// <summary>A value that may be absent - replaces null for "maybe" results such as repository lookups.</summary>
public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly T? _value;

    private Option(T value)
    {
        _value = value;
        IsSome = true;
    }

    public bool IsSome { get; }

    public bool IsNone => !IsSome;

    public static Option<T> None => default;

    public static Option<T> Some(T value) =>
        value is null ? throw new ArgumentNullException(nameof(value), "Option.Some needs a value; use None.") : new Option<T>(value);

    public static implicit operator Option<T>(OptionNone _) => default;

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) => IsSome ? some(_value!) : none();

    public void Switch(Action<T> some, Action none)
    {
        if (IsSome)
        {
            some(_value!);
        }
        else
        {
            none();
        }
    }

    public Option<TResult> Map<TResult>(Func<T, TResult> map) => IsSome ? Option<TResult>.Some(map(_value!)) : default;

    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> bind) => IsSome ? bind(_value!) : default;

    public Option<T> Filter(Func<T, bool> predicate) => IsSome && predicate(_value!) ? this : default;

    public Option<T> Tap(Action<T> action)
    {
        if (IsSome)
        {
            action(_value!);
        }

        return this;
    }

    public T GetValueOrDefault(T fallback) => IsSome ? _value! : fallback;

    public T GetValueOrDefault(Func<T> fallback) => IsSome ? _value! : fallback();

    public bool TryGetValue(out T value)
    {
        value = _value!;
        return IsSome;
    }

    /// <summary>Turns "absent" into an error (typically NotFound) to continue on the Result railway.</summary>
    public Result<T> ToResult(Error whenNone) => IsSome ? Result<T>.Success(_value!) : Result<T>.Failure(whenNone);

    public Result<T> ToResult(Func<Error> whenNone) => IsSome ? Result<T>.Success(_value!) : Result<T>.Failure(whenNone());

    public bool Equals(Option<T> other) =>
        IsSome == other.IsSome && (!IsSome || EqualityComparer<T>.Default.Equals(_value, other._value));

    public override bool Equals(object? obj) => obj is Option<T> other && Equals(other);

    public override int GetHashCode() => IsSome ? HashCode.Combine(true, _value) : 0;

    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);

    public override string ToString() => IsSome ? $"Some({_value})" : "None";
}

/// <summary>Marker that converts to <c>Option&lt;T&gt;.None</c> of any T.</summary>
public readonly struct OptionNone;

public static class Option
{
    public static OptionNone None => default;

    public static Option<T> Some<T>(T value) => Option<T>.Some(value);

    public static Option<T> FromNullable<T>(T? value)
        where T : class => value is null ? default : Option<T>.Some(value);

    public static Option<T> FromNullable<T>(T? value)
        where T : struct => value is null ? default : Option<T>.Some(value.Value);

    /// <summary>Lookup in a dictionary as Option.</summary>
    public static Option<TValue> Find<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, TKey key) =>
        source.TryGetValue(key, out var value) && value is not null ? Option<TValue>.Some(value) : default;

    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            return item is null ? default : Option<T>.Some(item);
        }

        return default;
    }

    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate) => source.Where(predicate).FirstOrNone();
}
