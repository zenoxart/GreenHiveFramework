using GreenHive.NET.Contracts;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Security;

/// <summary>
/// Turns errors into localized text. Messages are TextKeys, optionally with arguments separated by "|"
/// ("validation:maxLength|500" -> text of validation:maxLength with {0} = 500); unknown keys fall back to the code.
/// </summary>
public static class ErrorMessages
{
    public static string Text(LanguageManager languages, string message)
    {
        var parts = message.Split('|');
        if (!TextKey.IsTextKey(parts[0]))
        {
            return message;
        }

        var text = languages.TryGetText(parts[0], out var resolved) ? resolved : parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            text = text.Replace("{" + (i - 1) + "}", parts[i], StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>One line for an error (with the first field errors for validation failures).</summary>
    public static string Format(LanguageManager languages, Error error)
    {
        var text = Text(languages, error.Message);
        if (error.Fields.Count > 0)
        {
            var fields = error.Fields.Take(4).Select(f => $"{f.Key}: {string.Join(", ", f.Value.Select(v => Text(languages, v)))}");
            return $"{text} ({string.Join("; ", fields)})";
        }

        return text == error.Message && !TextKey.IsTextKey(error.Message) ? text : $"{text} [{error.Code}]";
    }

    /// <summary>Field errors by field name (for showing them next to form fields).</summary>
    public static IReadOnlyDictionary<string, string> Fields(LanguageManager languages, Error error) =>
        error.Fields.ToDictionary(f => f.Key, f => string.Join(" ", f.Value.Select(v => Text(languages, v))), StringComparer.OrdinalIgnoreCase);
}
