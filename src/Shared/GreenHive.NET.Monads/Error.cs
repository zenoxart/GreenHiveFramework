namespace GreenHive.NET.Monads;

/// <summary>Category of a failure; decides the HTTP status and the reaction of the workflow engine.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    Unavailable,
    Timeout,
    Unexpected,
}

/// <summary>
/// A failure as an immutable value.
/// <para><see cref="Code"/> is stable and machine-readable ("documents.invoice.notSubmitted");
/// <see cref="Message"/> is a TextKey or a plain fallback text shown to the user.</para>
/// </summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFields = new Dictionary<string, string[]>();
    private static readonly IReadOnlyDictionary<string, object?> NoMetadata = new Dictionary<string, object?>();

    /// <summary>Field errors: field name -> list of messages (TextKeys).</summary>
    public IReadOnlyDictionary<string, string[]> Fields { get; init; } = NoFields;

    /// <summary>Additional machine-readable information (never secrets).</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = NoMetadata;

    /// <summary>The causing error, if any. Not sent to clients in production.</summary>
    public Error? Inner { get; init; }

    /// <summary>Single errors combined by <see cref="Many"/>.</summary>
    public IReadOnlyList<Error> Errors { get; init; } = [];

    /// <summary>Exception summary for diagnostics (type and message only).</summary>
    public string? ExceptionDetails { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? fields = null) =>
        new(ErrorKind.Validation, code, message) { Fields = fields ?? NoFields };

    public static Error Field(string field, string code, string message) =>
        new(ErrorKind.Validation, code, message) { Fields = new Dictionary<string, string[]> { [field] = [message] } };

    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);

    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);

    public static Error Unauthorized(string code, string message) => new(ErrorKind.Unauthorized, code, message);

    public static Error Unavailable(string code, string message) => new(ErrorKind.Unavailable, code, message);

    public static Error Timeout(string code, string message) => new(ErrorKind.Timeout, code, message);

    public static Error Unexpected(string code, string message, Exception? exception = null) =>
        new(ErrorKind.Unexpected, code, message)
        {
            ExceptionDetails = exception is null ? null : $"{exception.GetType().FullName}: {exception.Message}",
        };

    /// <summary>
    /// Combines several errors. Validation errors are merged into one Validation error with all field errors;
    /// otherwise the most severe kind wins and all errors are kept in <see cref="Errors"/>.
    /// </summary>
    public static Error Many(IEnumerable<Error> errors)
    {
        var list = errors.SelectMany(e => e.Errors.Count > 0 ? e.Errors : [e]).ToList();
        if (list.Count == 0)
        {
            return Unexpected("errors.empty", "No error information.");
        }

        if (list.Count == 1)
        {
            return list[0];
        }

        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var error in list)
        {
            foreach (var (field, messages) in error.Fields)
            {
                fields[field] = fields.TryGetValue(field, out var existing) ? [.. existing, .. messages] : messages;
            }
        }

        var kind = list.All(e => e.Kind == ErrorKind.Validation) ? ErrorKind.Validation : list.Max(e => e.Kind);
        var first = list.First(e => e.Kind == kind);
        return new Error(kind, kind == ErrorKind.Validation ? "validation.failed" : first.Code, first.Message)
        {
            Fields = fields,
            Errors = list,
        };
    }

    public Error WithInner(Error inner) => this with { Inner = inner };

    public Error WithMetadata(string key, object? value) =>
        this with { Metadata = new Dictionary<string, object?>(Metadata) { [key] = value } };

    public override string ToString() => ExceptionDetails is null ? $"{Kind} {Code}: {Message}" : $"{Kind} {Code}: {Message} ({ExceptionDetails})";
}
