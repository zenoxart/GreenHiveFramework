using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GreenHive.NET.Monads;

namespace GreenHive.NET.WorkflowContracts;

/// <summary>
/// Validates and normalizes form values against an InputSchema (required, types, ranges, patterns, options).
/// The same rules run in the client form, in the workflow engine and in the AI guardrails.
/// </summary>
public static class InputValidator
{
    public static Validation<JsonObject> Validate(InputSchema schema, JsonObject? values)
    {
        values ??= [];
        var result = new JsonObject();
        var errors = new List<Error>();

        foreach (var field in schema.Fields)
        {
            var raw = values.FirstOrDefault(kv => kv.Key.Equals(field.Name, StringComparison.OrdinalIgnoreCase)).Value;
            var text = raw is null ? null : raw is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : raw.ToJsonString();
            if (string.IsNullOrWhiteSpace(text) && field.Default is not null)
            {
                text = field.Default;
                raw = JsonValue.Create(field.Default);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                if (field.Required && field.Type != InputFieldType.Boolean)
                {
                    errors.Add(Error.Field(field.Name, $"workflow.input.{field.Name}.required", "validation:required"));
                }

                continue;
            }

            switch (field.Type)
            {
                case InputFieldType.Number:
                    if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    {
                        errors.Add(Error.Field(field.Name, $"workflow.input.{field.Name}.number", "validation:number"));
                    }
                    else if ((field.Min is { } min && number < min) || (field.Max is { } max && number > max))
                    {
                        errors.Add(Error.Field(field.Name, $"workflow.input.{field.Name}.range", $"validation:range|{field.Min}|{field.Max}"));
                    }
                    else
                    {
                        result[field.Name] = number;
                    }

                    break;
                case InputFieldType.Boolean:
                    result[field.Name] = text is "true" or "True" or "1" or "yes";
                    break;
                case InputFieldType.Date:
                    if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                    {
                        errors.Add(Error.Field(field.Name, $"workflow.input.{field.Name}.date", "validation:date"));
                    }
                    else
                    {
                        result[field.Name] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }

                    break;
                case InputFieldType.Choice:
                    if (field.Options.Count > 0 && !field.Options.Any(o => o.Value.Equals(text, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add(Error.Field(field.Name, $"workflow.input.{field.Name}.option", "validation:oneOf"));
                    }
                    else
                    {
                        result[field.Name] = field.Options.FirstOrDefault(o => o.Value.Equals(text, StringComparison.OrdinalIgnoreCase))?.Value ?? text;
                    }

                    break;
                default:
                    if (field.MaxLength is { } maxLength && text.Length > maxLength)
                    {
                        errors.Add(Error.Field(field.Name, $"workflow.input.{field.Name}.maxLength", $"validation:maxLength|{maxLength}"));
                    }
                    else if (field.Pattern is { } pattern && !Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                    {
                        errors.Add(Error.Field(field.Name, $"workflow.input.{field.Name}.pattern", "validation:pattern"));
                    }
                    else
                    {
                        result[field.Name] = raw is JsonValue ? text : raw?.DeepClone();
                    }

                    break;
            }
        }

        // Keep values that are not part of the schema (e.g. technical IDs set by mappings).
        foreach (var (key, value) in values)
        {
            if (schema.Find(key) is null && !result.ContainsKey(key))
            {
                result[key] = value?.DeepClone();
            }
        }

        return errors.Count == 0 ? Validation<JsonObject>.Valid(result) : Validation<JsonObject>.Invalid(errors);
    }

    /// <summary>Copy of the values with sensitive fields masked (logs, inbox, AI context).</summary>
    public static JsonObject Mask(JsonObject values, Func<string, bool> isSensitive)
    {
        var copy = new JsonObject();
        foreach (var (key, value) in values)
        {
            copy[key] = isSensitive(key) ? "•••" : value?.DeepClone();
        }

        return copy;
    }
}
