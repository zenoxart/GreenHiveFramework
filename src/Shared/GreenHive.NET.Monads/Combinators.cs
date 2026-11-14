namespace GreenHive.NET.Monads;

/// <summary>Helpers for collections and parallel work on Results and Validations.</summary>
public static class Combinators
{
    /// <summary>List of Results -> Result of list; the first failure wins.</summary>
    public static Result<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        var list = new List<T>();
        foreach (var result in results)
        {
            if (!result.TryGetValue(out var value, out var error))
            {
                return Result<IReadOnlyList<T>>.Failure(error);
            }

            list.Add(value);
        }

        return list;
    }

    public static Result<IReadOnlyList<TResult>> Traverse<T, TResult>(this IEnumerable<T> source, Func<T, Result<TResult>> map) =>
        source.Select(map).Sequence();

    public static async Task<Result<IReadOnlyList<TResult>>> TraverseAsync<T, TResult>(
        this IEnumerable<T> source, Func<T, Task<Result<TResult>>> map)
    {
        var list = new List<TResult>();
        foreach (var item in source)
        {
            var result = await map(item).ConfigureAwait(false);
            if (!result.TryGetValue(out var value, out var error))
            {
                return Result<IReadOnlyList<TResult>>.Failure(error);
            }

            list.Add(value);
        }

        return list;
    }

    /// <summary>List of Validations -> Validation of list with all failures collected.</summary>
    public static Validation<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Validation<T>> validations)
    {
        var values = new List<T>();
        var errors = new List<Error>();
        foreach (var validation in validations)
        {
            if (validation.IsValid)
            {
                values.Add(validation.Match(v => v, _ => default!));
            }
            else
            {
                errors.AddRange(validation.Errors);
            }
        }

        return errors.Count == 0 ? Validation<IReadOnlyList<T>>.Valid(values) : Validation<IReadOnlyList<T>>.Invalid(errors);
    }

    public static Result<(T1, T2)> Combine<T1, T2>(Result<T1> r1, Result<T2> r2) =>
        from a in r1
        from b in r2
        select (a, b);

    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(Result<T1> r1, Result<T2> r2, Result<T3> r3) =>
        from a in r1
        from b in r2
        from c in r3
        select (a, b, c);

    /// <summary>Awaits independent async results in parallel; the first failure (in argument order) wins.</summary>
    public static async Task<Result<(T1, T2)>> WhenAll<T1, T2>(Task<Result<T1>> t1, Task<Result<T2>> t2)
    {
        await Task.WhenAll(t1, t2).ConfigureAwait(false);
        return Combine(t1.Result, t2.Result);
    }

    public static async Task<Result<(T1, T2, T3)>> WhenAll<T1, T2, T3>(Task<Result<T1>> t1, Task<Result<T2>> t2, Task<Result<T3>> t3)
    {
        await Task.WhenAll(t1, t2, t3).ConfigureAwait(false);
        return Combine(t1.Result, t2.Result, t3.Result);
    }

    /// <summary>Tries alternatives in order (e.g. cache, then database); returns the first success or the last error.</summary>
    public static Result<T> FirstSuccess<T>(params Func<Result<T>>[] alternatives)
    {
        Error? last = null;
        foreach (var alternative in alternatives)
        {
            var result = alternative();
            if (result.IsSuccess)
            {
                return result;
            }

            last = result.Error;
        }

        return Result<T>.Failure(last ?? Error.NotFound("alternatives.empty", "errors:notFound"));
    }

    public static async Task<Result<T>> FirstSuccessAsync<T>(params Func<Task<Result<T>>>[] alternatives)
    {
        Error? last = null;
        foreach (var alternative in alternatives)
        {
            var result = await alternative().ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return result;
            }

            last = result.Error;
        }

        return Result<T>.Failure(last ?? Error.NotFound("alternatives.empty", "errors:notFound"));
    }
}
