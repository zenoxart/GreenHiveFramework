namespace GreenHive.NET.Contracts;

/// <summary>
/// Refers to a localized text by key: "&lt;scope&gt;:&lt;key&gt;", e.g. "shell:command.save.label".
/// Scope = "shell" or a module ID; the key is dot-separated.
/// Conventions: &lt;key&gt;.label, &lt;key&gt;.tooltip, &lt;key&gt;.description, &lt;key&gt;.disabled.&lt;reason&gt;.
/// </summary>
public readonly record struct TextKey(string Scope, string Key)
{
    public static TextKey Parse(string text)
    {
        var colon = text.IndexOf(':');
        return colon <= 0 ? new TextKey("shell", text) : new TextKey(text[..colon], text[(colon + 1)..]);
    }

    public static bool IsTextKey(string? text) =>
        text is not null && text.IndexOf(':') is > 0 and var colon && colon < text.Length - 1 && !text.Contains(' ');

    public TextKey Label => this with { Key = $"{Key}.label" };

    public TextKey ToolTip => this with { Key = $"{Key}.tooltip" };

    public TextKey Description => this with { Key = $"{Key}.description" };

    public TextKey Disabled(string reason) => this with { Key = $"{Key}.disabled.{reason}" };

    public override string ToString() => $"{Scope}:{Key}";

    public static implicit operator string(TextKey key) => key.ToString();
}
