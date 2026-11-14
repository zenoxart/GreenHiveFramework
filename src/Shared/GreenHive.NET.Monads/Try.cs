using System.Data.Common;
using System.Net;

namespace GreenHive.NET.Monads;

/// <summary>
/// Boundary between exception-throwing code (framework, database driver, HTTP client) and the monadic world.
/// Used only in adapters; domain and use-case code never catch exceptions.
/// </summary>
public static class Try
{
    private static readonly List<Func<Exception, Error?>> Mappers = [];

    /// <summary>Registers an additional exception mapper (checked before the built-in rules).</summary>
    public static void AddMapper(Func<Exception, Error?> mapper)
    {
        lock (Mappers)
        {
            Mappers.Insert(0, mapper);
        }
    }

    public static Result<T> Run<T>(Func<T> action, string code = "operation.failed")
    {
        try
        {
            return Result<T>.Success(action());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(Map(ex, code));
        }
    }

    public static Result<Unit> Run(Action action, string code = "operation.failed")
    {
        try
        {
            action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Map(ex, code));
        }
    }

    public static async Task<Result<T>> RunAsync<T>(Func<Task<T>> action, string code = "operation.failed")
    {
        try
        {
            return Result<T>.Success(await action().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(Map(ex, code));
        }
    }

    public static async Task<Result<Unit>> RunAsync(Func<Task> action, string code = "operation.failed")
    {
        try
        {
            await action().ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Map(ex, code));
        }
    }

    /// <summary>Runs a call that itself returns a Result; exceptions become errors.</summary>
    public static async Task<Result<T>> RunAsync<T>(Func<Task<Result<T>>> action, string code = "operation.failed")
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(Map(ex, code));
        }
    }

    /// <summary>Maps an exception to an error of the right kind.</summary>
    public static Error Map(Exception exception, string code = "operation.failed")
    {
        Func<Exception, Error?>[] mappers;
        lock (Mappers)
        {
            mappers = [.. Mappers];
        }

        foreach (var mapper in mappers)
        {
            if (mapper(exception) is { } mapped)
            {
                return mapped;
            }
        }

        return exception switch
        {
            TimeoutException => Error.Timeout($"{code}.timeout", "errors:timeout"),
            TaskCanceledException { InnerException: TimeoutException } => Error.Timeout($"{code}.timeout", "errors:timeout"),
            OperationCanceledException => Error.Timeout($"{code}.cancelled", "errors:cancelled"),
            HttpRequestException { StatusCode: HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout } =>
                Error.Unavailable($"{code}.unavailable", "errors:unavailable"),
            HttpRequestException { StatusCode: null } => Error.Unavailable($"{code}.unreachable", "errors:unreachable"),
            DbException db when db.SqlState is "23505" => Error.Conflict($"{code}.duplicate", "errors:duplicate"),
            DbException db when db.SqlState is "40001" or "40P01" => Error.Conflict($"{code}.concurrency", "errors:concurrency"),
            DbException db when db.SqlState is "23503" => Error.Conflict($"{code}.reference", "errors:reference"),
            DbException db when db.SqlState is "57014" => Error.Timeout($"{code}.timeout", "errors:timeout"),
            DbException db when db.SqlState?.StartsWith("08", StringComparison.Ordinal) == true =>
                Error.Unavailable($"{code}.database", "errors:unavailable"),
            _ => Error.Unexpected(code, "errors:unexpected", exception),
        };
    }
}
