namespace GreenHive.NET.Monads;

/// <summary>
/// The central monad: either a success with a value or a failure with an <see cref="Error"/>.
/// There is no throwing <c>.Value</c>: the value is only reachable together with the failure case
/// (<see cref="Match{TResult}"/>, <see cref="TryGetValue(out T, out Error)"/>).
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error ?? throw new ArgumentNullException(nameof(error));
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The error of a failure; for a success (or a default instance) an "uninitialized" error.</summary>
    public Error Error => _error ?? (IsSuccess ? throw new InvalidOperationException("A successful result has no error.") : Uninitialized);

    private static Error Uninitialized { get; } = Error.Unexpected("result.uninitialized", "The result was not initialized.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);

    public bool TryGetValue(out T value, out Error error)
    {
        value = _value!;
        error = IsSuccess ? null! : Error;
        return IsSuccess;
    }

    public bool TryGetValue(out T value)
    {
        value = _value!;
        return IsSuccess;
    }

    public TResult Match<TResult>(Func<T, TResult> success, Func<Error, TResult> failure) =>
        IsSuccess ? success(_value!) : failure(Error);

    public void Switch(Action<T> success, Action<Error> failure)
    {
        if (IsSuccess)
        {
            success(_value!);
        }
        else
        {
            failure(Error);
        }
    }

    public Result<TResult> Map<TResult>(Func<T, TResult> map) =>
        IsSuccess ? Result<TResult>.Success(map(_value!)) : Result<TResult>.Failure(Error);

    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> bind) =>
        IsSuccess ? bind(_value!) : Result<TResult>.Failure(Error);

    public Result<T> MapError(Func<Error, Error> map) => IsSuccess ? this : Failure(map(Error));

    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        IsSuccess && !predicate(_value!) ? Failure(error) : this;

    public Result<T> Ensure(Func<T, bool> predicate, Func<T, Error> error) =>
        IsSuccess && !predicate(_value!) ? Failure(error(_value!)) : this;

    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
        {
            action(_value!);
        }

        return this;
    }

    public Result<T> TapError(Action<Error> action)
    {
        if (IsFailure)
        {
            action(Error);
        }

        return this;
    }

    /// <summary>Turns failures into a success, e.g. NotFound -> default value.</summary>
    public Result<T> Recover(Func<Error, Result<T>> recover) => IsSuccess ? this : recover(Error);

    public Result<T> Recover(ErrorKind kind, Func<Error, T> recover) =>
        IsFailure && Error.Kind == kind ? Success(recover(Error)) : this;

    public Result<Unit> ToUnit() => IsSuccess ? Result<Unit>.Success(Unit.Value) : Result<Unit>.Failure(Error);

    public Option<T> ToOption() => IsSuccess && _value is not null ? Option<T>.Some(_value) : default;

    public T GetValueOrDefault(T fallback) => IsSuccess ? _value! : fallback;

    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({Error})";
}

public static class Result
{
    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static Result<Unit> Failure(Error error) => Result<Unit>.Failure(error);

    /// <summary>Success if the condition holds, otherwise the error.</summary>
    public static Result<Unit> Require(bool condition, Error error) => condition ? Success() : Failure(error);

    public static Result<T> FromOption<T>(Option<T> option, Error whenNone) => option.ToResult(whenNone);
}
