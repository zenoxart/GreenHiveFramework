using System.Text.Json.Serialization;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Contracts;

/// <summary>
/// Uniform error format between backend and client, based on RFC 9457 (Problem Details).
/// It is the wire format of <see cref="Error"/>: the client turns it back into an Error, so client code uses Result too.
/// </summary>
public sealed record ApiError
{
    public string Type { get; init; } = "about:blank";

    public string Title { get; init; } = string.Empty;

    public int Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    /// <summary>Stable machine-readable code, prefixed with the module or service.</summary>
    public string Code { get; init; } = "unknown";

    /// <summary>TextKey (or fallback text) of the user-facing message.</summary>
    public string Message { get; init; } = string.Empty;

    public ErrorKind Kind { get; init; } = ErrorKind.Unexpected;

    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public string? CorrelationId { get; init; }

    /// <summary>Single errors of a combined error.</summary>
    public IReadOnlyList<ApiError>? Errors { get; init; }

    public static int StatusOf(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => 400,
        ErrorKind.Unauthorized => 401,
        ErrorKind.Forbidden => 403,
        ErrorKind.NotFound => 404,
        ErrorKind.Conflict => 409,
        ErrorKind.Unavailable => 503,
        ErrorKind.Timeout => 504,
        _ => 500,
    };

    public static ErrorKind KindOf(int status) => status switch
    {
        400 or 422 => ErrorKind.Validation,
        401 => ErrorKind.Unauthorized,
        403 => ErrorKind.Forbidden,
        404 => ErrorKind.NotFound,
        409 or 412 => ErrorKind.Conflict,
        502 or 503 => ErrorKind.Unavailable,
        408 or 504 => ErrorKind.Timeout,
        _ => ErrorKind.Unexpected,
    };

    /// <summary>Creates the wire format of an error. Internal details are only included when requested (development).</summary>
    public static ApiError FromError(Error error, string? correlationId = null, bool includeDetails = false) => new()
    {
        Type = $"https://greenhive.dev/errors/{error.Kind.ToString().ToLowerInvariant()}",
        Title = error.Kind.ToString(),
        Status = StatusOf(error.Kind),
        Code = error.Code,
        Message = error.Message,
        Kind = error.Kind,
        Detail = includeDetails ? error.ExceptionDetails ?? error.Inner?.ToString() : null,
        Fields = error.Fields.Count > 0 ? error.Fields : null,
        CorrelationId = correlationId,
        Errors = error.Errors.Count > 1 ? error.Errors.Select(e => FromError(e, null, includeDetails)).ToList() : null,
    };

    /// <summary>Turns the wire format back into an error.</summary>
    public Error ToError() => new(Kind == ErrorKind.Unexpected && Status is > 0 and not 500 ? KindOf(Status) : Kind, Code, Message)
    {
        Fields = Fields ?? new Dictionary<string, string[]>(),
        Errors = Errors?.Select(e => e.ToError()).ToList() ?? [],
        ExceptionDetails = Detail,
    };
}

/// <summary>A page of items.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take)
{
    [JsonIgnore]
    public bool HasMore => Skip + Items.Count < Total;
}
