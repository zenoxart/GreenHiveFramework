using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Backend.Core.Data;

/// <summary>
/// Data access in the monad architecture (plain ADO.NET over a <see cref="DbDataSource"/>):
/// lookups return <c>Option</c>, lists return (possibly empty) lists, writes return <c>Result</c>; database exceptions are
/// converted with <see cref="Try"/> (unique constraint -> Conflict, timeout -> Timeout, connection -> Unavailable).
/// Parameters are passed as an anonymous object (<c>new { id, name }</c>) and referenced as <c>@id</c>, <c>@name</c>.
/// JSON is passed as string and cast in SQL (<c>@payload::jsonb</c>).
/// </summary>
public sealed class Db(DbDataSource dataSource)
{
    public DbDataSource DataSource { get; } = dataSource;

    public Task<Result<IReadOnlyList<T>>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, object? parameters = null, CancellationToken ct = default) =>
        Try.RunAsync(async () =>
        {
            await using var connection = await DataSource.OpenConnectionAsync(ct);
            return await DbSession.Query(connection, null, sql, map, parameters, ct);
        }, "database.query");

    public async Task<Result<Option<T>>> QuerySingleAsync<T>(string sql, Func<DbDataReader, T> map, object? parameters = null, CancellationToken ct = default) =>
        (await QueryAsync(sql, map, parameters, ct)).Map(list => list.FirstOrNone());

    /// <summary>Loads one row and turns "absent" into the given error (typically NotFound).</summary>
    public Task<Result<T>> FindAsync<T>(string sql, Func<DbDataReader, T> map, object? parameters, Error notFound, CancellationToken ct = default) =>
        QuerySingleAsync(sql, map, parameters, ct).Bind(option => option.ToResult(notFound));

    public Task<Result<int>> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default) =>
        Try.RunAsync(async () =>
        {
            await using var connection = await DataSource.OpenConnectionAsync(ct);
            return await DbSession.Execute(connection, null, sql, parameters, ct);
        }, "database.execute");

    public Task<Result<T?>> ScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        Try.RunAsync(async () =>
        {
            await using var connection = await DataSource.OpenConnectionAsync(ct);
            return await DbSession.Scalar<T>(connection, null, sql, parameters, ct);
        }, "database.scalar");

    /// <summary>
    /// Runs the inner pipeline in a transaction: commits only on Success, rolls back on Failure (no exceptions needed).
    /// </summary>
    public Task<Result<T>> InTransactionAsync<T>(Func<DbSession, Task<Result<T>>> work, IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default) =>
        Try.RunAsync(async () =>
        {
            await using var connection = await DataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(isolation, ct);
            var session = new DbSession(connection, transaction);
            var result = await work(session);
            if (result.IsSuccess)
            {
                await transaction.CommitAsync(ct);
            }
            else
            {
                await transaction.RollbackAsync(ct);
            }

            return result;
        }, "database.transaction");

    /// <summary>Checks that the database is reachable (readiness probe).</summary>
    public async Task<bool> PingAsync(CancellationToken ct = default) =>
        (await ScalarAsync<int>("select 1", null, ct)).IsSuccess;
}

/// <summary>A connection with an open transaction; all calls run inside it.</summary>
public sealed class DbSession(DbConnection connection, DbTransaction? transaction)
{
    public DbConnection Connection { get; } = connection;

    public DbTransaction? Transaction { get; } = transaction;

    public Task<Result<IReadOnlyList<T>>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, object? parameters = null, CancellationToken ct = default) =>
        Try.RunAsync(() => Query(Connection, Transaction, sql, map, parameters, ct), "database.query");

    public async Task<Result<Option<T>>> QuerySingleAsync<T>(string sql, Func<DbDataReader, T> map, object? parameters = null, CancellationToken ct = default) =>
        (await QueryAsync(sql, map, parameters, ct)).Map(list => list.FirstOrNone());

    public Task<Result<T>> FindAsync<T>(string sql, Func<DbDataReader, T> map, object? parameters, Error notFound, CancellationToken ct = default) =>
        QuerySingleAsync(sql, map, parameters, ct).Bind(option => option.ToResult(notFound));

    public Task<Result<int>> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default) =>
        Try.RunAsync(() => Execute(Connection, Transaction, sql, parameters, ct), "database.execute");

    /// <summary>Executes and expects exactly <paramref name="expectedRows"/> affected rows, otherwise a concurrency conflict.</summary>
    public Task<Result<Unit>> ExecuteExpectingAsync(string sql, object? parameters, int expectedRows, Error onMismatch, CancellationToken ct = default) =>
        ExecuteAsync(sql, parameters, ct).Bind(rows => rows == expectedRows ? Result.Success() : Result.Failure(onMismatch));

    public Task<Result<T?>> ScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        Try.RunAsync(() => Scalar<T>(Connection, Transaction, sql, parameters, ct), "database.scalar");

    internal static async Task<IReadOnlyList<T>> Query<T>(
        DbConnection connection, DbTransaction? transaction, string sql, Func<DbDataReader, T> map, object? parameters, CancellationToken ct)
    {
        await using var command = Create(connection, transaction, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(map(reader));
        }

        return list;
    }

    internal static async Task<int> Execute(DbConnection connection, DbTransaction? transaction, string sql, object? parameters, CancellationToken ct)
    {
        await using var command = Create(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(ct);
    }

    internal static async Task<T?> Scalar<T>(DbConnection connection, DbTransaction? transaction, string sql, object? parameters, CancellationToken ct)
    {
        await using var command = Create(connection, transaction, sql, parameters);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? default : value is T typed ? typed : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    private static DbCommand Create(DbConnection connection, DbTransaction? transaction, string sql, object? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in Parameters(parameters))
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = Normalize(value);
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static IEnumerable<(string Name, object? Value)> Parameters(object? parameters) => parameters switch
    {
        null => [],
        IReadOnlyDictionary<string, object?> dictionary => dictionary.Select(kv => (kv.Key, kv.Value)),
        IDictionary dictionary => dictionary.Keys.Cast<object>().Select(k => (k.ToString()!, dictionary[k])),
        _ => parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (p.Name, p.GetValue(parameters))),
    };

    private static object Normalize(object? value) => value switch
    {
        null => DBNull.Value,
        DateTimeOffset dto => dto.ToUniversalTime(),
        DateTime dt => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc),
        Enum e => e.ToString(),
        _ => value,
    };
}

/// <summary>Null-safe reader helpers.</summary>
public static class DbReaderExtensions
{
    public static T Get<T>(this DbDataReader reader, string column) => reader.GetFieldValue<T>(reader.GetOrdinal(column));

    public static T? GetOrNull<T>(this DbDataReader reader, string column)
        where T : class
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
    }

    public static T? GetNullable<T>(this DbDataReader reader, string column)
        where T : struct
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
    }

    public static string GetText(this DbDataReader reader, string column) => reader.GetOrNull<string>(column) ?? string.Empty;

    public static TEnum GetEnum<TEnum>(this DbDataReader reader, string column)
        where TEnum : struct, Enum => Enum.Parse<TEnum>(reader.Get<string>(column), ignoreCase: true);

    public static DateTimeOffset GetTime(this DbDataReader reader, string column) => reader.Get<DateTimeOffset>(column);

    public static DateTimeOffset? GetTimeOrNull(this DbDataReader reader, string column) => reader.GetNullable<DateTimeOffset>(column);

    public static string[] GetTextArray(this DbDataReader reader, string column) => reader.GetOrNull<string[]>(column) ?? [];
}
