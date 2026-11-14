namespace GreenHive.NET.Monads;

/// <summary>Async versions of all Result/Option operations, so async pipelines read like sync ones.</summary>
public static class AsyncExtensions
{
    // ---------- Result<T> -> async continuation ----------

    public static async Task<Result<TResult>> BindAsync<T, TResult>(this Result<T> result, Func<T, Task<Result<TResult>>> bind) =>
        result.TryGetValue(out var value, out var error) ? await bind(value).ConfigureAwait(false) : Result<TResult>.Failure(error);

    public static async Task<Result<TResult>> MapAsync<T, TResult>(this Result<T> result, Func<T, Task<TResult>> map) =>
        result.TryGetValue(out var value, out var error) ? Result<TResult>.Success(await map(value).ConfigureAwait(false)) : Result<TResult>.Failure(error);

    public static async Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> action)
    {
        if (result.TryGetValue(out var value))
        {
            await action(value).ConfigureAwait(false);
        }

        return result;
    }

    public static async Task<Result<T>> EnsureAsync<T>(this Result<T> result, Func<T, Task<bool>> predicate, Error error) =>
        result.TryGetValue(out var value) && !await predicate(value).ConfigureAwait(false) ? Result<T>.Failure(error) : result;

    // ---------- Task<Result<T>> ----------

    public static async Task<Result<TResult>> Map<T, TResult>(this Task<Result<T>> task, Func<T, TResult> map) =>
        (await task.ConfigureAwait(false)).Map(map);

    public static async Task<Result<TResult>> MapAsync<T, TResult>(this Task<Result<T>> task, Func<T, Task<TResult>> map) =>
        await (await task.ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

    public static async Task<Result<TResult>> Bind<T, TResult>(this Task<Result<T>> task, Func<T, Result<TResult>> bind) =>
        (await task.ConfigureAwait(false)).Bind(bind);

    public static async Task<Result<TResult>> BindAsync<T, TResult>(this Task<Result<T>> task, Func<T, Task<Result<TResult>>> bind) =>
        await (await task.ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> task, Func<T, bool> predicate, Error error) =>
        (await task.ConfigureAwait(false)).Ensure(predicate, error);

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> task, Func<T, bool> predicate, Func<T, Error> error) =>
        (await task.ConfigureAwait(false)).Ensure(predicate, error);

    public static async Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> task, Func<T, Task<bool>> predicate, Error error) =>
        await (await task.ConfigureAwait(false)).EnsureAsync(predicate, error).ConfigureAwait(false);

    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> task, Action<T> action) =>
        (await task.ConfigureAwait(false)).Tap(action);

    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> task, Func<T, Task> action) =>
        await (await task.ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> task, Action<Error> action) =>
        (await task.ConfigureAwait(false)).TapError(action);

    public static async Task<Result<T>> MapError<T>(this Task<Result<T>> task, Func<Error, Error> map) =>
        (await task.ConfigureAwait(false)).MapError(map);

    public static async Task<Result<T>> Recover<T>(this Task<Result<T>> task, Func<Error, Result<T>> recover) =>
        (await task.ConfigureAwait(false)).Recover(recover);

    public static async Task<TResult> Match<T, TResult>(this Task<Result<T>> task, Func<T, TResult> success, Func<Error, TResult> failure) =>
        (await task.ConfigureAwait(false)).Match(success, failure);

    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> task, Func<T, Task<TResult>> success, Func<Error, Task<TResult>> failure)
    {
        var result = await task.ConfigureAwait(false);
        return result.TryGetValue(out var value, out var error)
            ? await success(value).ConfigureAwait(false)
            : await failure(error).ConfigureAwait(false);
    }

    public static async Task<Result<Unit>> ToUnit<T>(this Task<Result<T>> task) => (await task.ConfigureAwait(false)).ToUnit();

    // ---------- Option ----------

    public static async Task<Result<T>> ToResult<T>(this Task<Option<T>> task, Error whenNone) =>
        (await task.ConfigureAwait(false)).ToResult(whenNone);

    public static async Task<Result<T>> ToResult<T>(this Task<Option<T>> task, Func<Error> whenNone) =>
        (await task.ConfigureAwait(false)).ToResult(whenNone);

    public static async Task<Option<TResult>> Map<T, TResult>(this Task<Option<T>> task, Func<T, TResult> map) =>
        (await task.ConfigureAwait(false)).Map(map);

    // ---------- Validation ----------

    public static async Task<Result<T>> ToResult<T>(this Task<Validation<T>> task) => (await task.ConfigureAwait(false)).ToResult();
}
