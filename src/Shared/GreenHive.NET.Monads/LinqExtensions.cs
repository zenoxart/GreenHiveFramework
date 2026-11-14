namespace GreenHive.NET.Monads;

/// <summary>
/// Select / SelectMany / Where for Option, Result and Task&lt;Result&gt;, so pipelines that need several earlier values
/// can use query syntax (from ... in ... select ...).
/// </summary>
public static class LinqExtensions
{
    // Option
    public static Option<TResult> Select<T, TResult>(this Option<T> option, Func<T, TResult> map) => option.Map(map);

    public static Option<TResult> SelectMany<T, TMiddle, TResult>(
        this Option<T> option, Func<T, Option<TMiddle>> bind, Func<T, TMiddle, TResult> project) =>
        option.Bind(a => bind(a).Map(b => project(a, b)));

    public static Option<T> Where<T>(this Option<T> option, Func<T, bool> predicate) => option.Filter(predicate);

    // Result
    public static Result<TResult> Select<T, TResult>(this Result<T> result, Func<T, TResult> map) => result.Map(map);

    public static Result<TResult> SelectMany<T, TMiddle, TResult>(
        this Result<T> result, Func<T, Result<TMiddle>> bind, Func<T, TMiddle, TResult> project) =>
        result.Bind(a => bind(a).Map(b => project(a, b)));

    // Task<Result>
    public static Task<Result<TResult>> Select<T, TResult>(this Task<Result<T>> task, Func<T, TResult> map) => task.Map(map);

    public static Task<Result<TResult>> SelectMany<T, TMiddle, TResult>(
        this Task<Result<T>> task, Func<T, Task<Result<TMiddle>>> bind, Func<T, TMiddle, TResult> project) =>
        task.BindAsync(a => bind(a).Map(b => project(a, b)));

    public static Task<Result<TResult>> SelectMany<T, TMiddle, TResult>(
        this Task<Result<T>> task, Func<T, Result<TMiddle>> bind, Func<T, TMiddle, TResult> project) =>
        task.Bind(a => bind(a).Map(b => project(a, b)));

    public static Task<Result<TResult>> SelectMany<T, TMiddle, TResult>(
        this Result<T> result, Func<T, Task<Result<TMiddle>>> bind, Func<T, TMiddle, TResult> project) =>
        result.BindAsync(a => bind(a).Map(b => project(a, b)));
}
