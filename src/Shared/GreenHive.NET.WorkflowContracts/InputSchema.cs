namespace GreenHive.NET.WorkflowContracts;

public enum InputFieldType
{
    Text,
    MultiLine,
    Number,
    Date,
    Boolean,
    Choice,
    EntityRef,
    File,
}

public enum FieldSpan
{
    Full,
    Half,
}

/// <summary>One field of an input form.</summary>
public sealed record InputField(string Name, InputFieldType Type = InputFieldType.Text)
{
    /// <summary>TextKey or plain label.</summary>
    public string Label { get; init; } = string.Empty;

    public string? Help { get; init; }

    public string? Default { get; init; }

    public bool Required { get; init; }

    public decimal? Min { get; init; }

    public decimal? Max { get; init; }

    public int? MaxLength { get; init; }

    public string? Pattern { get; init; }

    public IReadOnlyList<ChoiceOption> Options { get; init; } = [];

    /// <summary>EntityRef: module API (relative to the gateway) returning [{ value, label }].</summary>
    public string? Lookup { get; init; }

    /// <summary>Masked in logs and in the AI context.</summary>
    public bool Sensitive { get; init; }

    public FieldSpan Span { get; init; } = FieldSpan.Full;

    public string? Group { get; init; }
}

/// <summary>
/// Describes the inputs of an Input step, so the client can generate the form and the AI agent knows what to provide.
/// Changing a field type, removing a field or making a field required is breaking (major version).
/// </summary>
public sealed record InputSchema(string SchemaId, int Version = 1)
{
    public IReadOnlyList<InputField> Fields { get; init; } = [];

    /// <summary>Optional server-side validation endpoint (relative to the gateway).</summary>
    public string? ValidationUrl { get; init; }

    public InputField? Find(string name) => Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
