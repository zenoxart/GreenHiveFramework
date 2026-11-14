using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GreenHive.NET.Contracts;

/// <summary>Strongly typed module ID: lowercase, dash-separated (e.g. "document-management").</summary>
[JsonConverter(typeof(ModuleIdJsonConverter))]
public readonly partial record struct ModuleId
{
    public ModuleId(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException($"'{value}' is not a valid module ID (lowercase letters, digits and dashes).", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    public static bool TryParse(string? value, out ModuleId id)
    {
        id = IsValid(value) ? new ModuleId(value!) : default;
        return id.Value is not null;
    }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(ModuleId id) => id.Value;

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();
}

public sealed class ModuleIdJsonConverter : JsonConverter<ModuleId>
{
    public override ModuleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, ModuleId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>Semantic version (major.minor.patch[-prerelease]) with SemVer ordering.</summary>
[JsonConverter(typeof(SemanticVersionJsonConverter))]
public sealed record SemanticVersion(int Major, int Minor, int Patch, string? PreRelease = null) : IComparable<SemanticVersion>
{
    public static SemanticVersion Zero { get; } = new(0, 0, 0);

    public static SemanticVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"'{text}' is not a semantic version.");

    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim().TrimStart('v', 'V');
        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        string? pre = null;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            pre = value[(dash + 1)..];
            value = value[..dash];
        }

        var parts = value.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < Math.Min(parts.Length, 3); i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        version = new SemanticVersion(numbers[0], numbers[1], numbers[2], string.IsNullOrEmpty(pre) ? null : pre);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var c = Major.CompareTo(other.Major);
        if (c == 0)
        {
            c = Minor.CompareTo(other.Minor);
        }

        if (c == 0)
        {
            c = Patch.CompareTo(other.Patch);
        }

        if (c != 0)
        {
            return c;
        }

        return (PreRelease, other.PreRelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(PreRelease, other.PreRelease),
        };
    }

    public static bool operator <(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) < 0;

    public static bool operator >(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) > 0;

    public static bool operator <=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) <= 0;

    public static bool operator >=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) >= 0;

    public override string ToString() => PreRelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}

public sealed class SemanticVersionJsonConverter : JsonConverter<SemanticVersion>
{
    public override SemanticVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SemanticVersion.Parse(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, SemanticVersion value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}

/// <summary>
/// Version range such as ">=1.2.0 &lt;3.0.0", "^1.2", "~1.4.0", "1.x", "1.2.3" or "*".
/// All comparators separated by spaces must hold.
/// </summary>
[JsonConverter(typeof(VersionRangeJsonConverter))]
public sealed record VersionRange(string Expression)
{
    public static VersionRange Any { get; } = new("*");

    public bool Satisfies(SemanticVersion version)
    {
        foreach (var part in Expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Check(part, version))
            {
                return false;
            }
        }

        return true;
    }

    public bool Satisfies(string version) => SemanticVersion.TryParse(version, out var v) && Satisfies(v);

    public static bool IsValid(string expression)
    {
        try
        {
            _ = new VersionRange(expression).Satisfies(SemanticVersion.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool Check(string comparator, SemanticVersion v)
    {
        if (comparator is "*" or "x" or "X")
        {
            return true;
        }

        if (comparator.StartsWith('^'))
        {
            var min = SemanticVersion.Parse(comparator[1..]);
            var max = min.Major > 0 ? new SemanticVersion(min.Major + 1, 0, 0) : new SemanticVersion(0, min.Minor + 1, 0);
            return v >= min && v < max;
        }

        if (comparator.StartsWith('~'))
        {
            var min = SemanticVersion.Parse(comparator[1..]);
            return v >= min && v < new SemanticVersion(min.Major, min.Minor + 1, 0);
        }

        if (comparator.EndsWith(".x", StringComparison.OrdinalIgnoreCase) || comparator.EndsWith(".*", StringComparison.Ordinal))
        {
            var parts = comparator[..^2].Split('.');
            var major = int.Parse(parts[0], CultureInfo.InvariantCulture);
            return parts.Length == 1
                ? v.Major == major
                : v.Major == major && v.Minor == int.Parse(parts[1], CultureInfo.InvariantCulture);
        }

        foreach (var op in new[] { ">=", "<=", ">", "<", "=" })
        {
            if (comparator.StartsWith(op, StringComparison.Ordinal))
            {
                var bound = SemanticVersion.Parse(comparator[op.Length..]);
                return op switch
                {
                    ">=" => v >= bound,
                    "<=" => v <= bound,
                    ">" => v > bound,
                    "<" => v < bound,
                    _ => v.CompareTo(bound) == 0,
                };
            }
        }

        return v.CompareTo(SemanticVersion.Parse(comparator)) == 0;
    }

    public override string ToString() => Expression;
}

public sealed class VersionRangeJsonConverter : JsonConverter<VersionRange>
{
    public override VersionRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString() ?? "*");

    public override void Write(Utf8JsonWriter writer, VersionRange value, JsonSerializerOptions options) => writer.WriteStringValue(value.Expression);
}

/// <summary>Tenant identifier.</summary>
public readonly record struct TenantId(Guid Value)
{
    /// <summary>The tenant used by the demo data.</summary>
    public static TenantId Demo { get; } = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    public override string ToString() => Value.ToString();
}

/// <summary>User identifier.</summary>
public readonly record struct UserId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
