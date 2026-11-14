namespace GreenHive.NET.CommandContracts;

/// <summary>Modifier keys of a <see cref="KeyGesture"/>.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}

/// <summary>Kind of a keyboard gesture.</summary>
public enum KeyGestureKind
{
    /// <summary>Executed once on key down.</summary>
    Single,

    /// <summary>Starts on key down and lasts while the modifier is held (e.g. Ctrl+Tab for the navigation wheel).</summary>
    Hold,
}

/// <summary>
/// A keyboard shortcut, independent of WPF types. Stored by virtual key code, so a binding works
/// on every keyboard layout. A two-step chord ("Ctrl+K, Ctrl+S") keeps its second step in <see cref="Then"/>.
/// </summary>
public sealed record KeyGesture(KeyModifiers Modifiers, int VirtualKey, KeyGestureKind Kind = KeyGestureKind.Single, KeyGesture? Then = null)
{
    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab"] = 9, ["Enter"] = 13, ["Escape"] = 27, ["Esc"] = 27, ["Space"] = 32,
        ["PageUp"] = 33, ["PageDown"] = 34, ["End"] = 35, ["Home"] = 36,
        ["Left"] = 37, ["Up"] = 38, ["Right"] = 39, ["Down"] = 40,
        ["Insert"] = 45, ["Delete"] = 46, ["Del"] = 46, ["Backspace"] = 8,
        ["Plus"] = 187, ["+"] = 187, ["Comma"] = 188, ["Minus"] = 189, ["-"] = 189,
        ["Period"] = 190, ["Slash"] = 191, ["/"] = 191,
    };

    private static readonly Dictionary<int, string> CanonicalNames = new()
    {
        [8] = "Backspace", [9] = "Tab", [13] = "Enter", [27] = "Esc", [32] = "Space",
        [33] = "PageUp", [34] = "PageDown", [35] = "End", [36] = "Home",
        [37] = "Left", [38] = "Up", [39] = "Right", [40] = "Down", [45] = "Insert", [46] = "Delete",
        [187] = "Plus", [188] = "Comma", [189] = "-", [190] = "Period", [191] = "/",
    };

    /// <summary>True if this gesture is a two-step chord.</summary>
    public bool IsChord => Then is not null;

    /// <summary>The first step of the gesture (the gesture itself when it is not a chord).</summary>
    public KeyGesture FirstPart => Then is null ? this : this with { Then = null };

    /// <summary>Parses text such as "Ctrl+Shift+P" or "Ctrl+K, Ctrl+S".</summary>
    public static KeyGesture Parse(string text) =>
        TryParse(text, out var gesture) && gesture is not null
            ? gesture
            : throw new FormatException($"'{text}' is not a valid key gesture.");

    /// <summary>Tries to parse a gesture text.</summary>
    public static bool TryParse(string? text, out KeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 2 || !TryParseSingle(parts[0], out var first))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            if (!TryParseSingle(parts[1], out var second))
            {
                return false;
            }

            first = first with { Then = second };
        }

        gesture = first;
        return true;
    }

    /// <summary>Name of a virtual key as used in gesture texts.</summary>
    public static string KeyName(int virtualKey)
    {
        if (CanonicalNames.TryGetValue(virtualKey, out var name))
        {
            return name;
        }

        if (virtualKey is >= 48 and <= 57 or >= 65 and <= 90)
        {
            return ((char)virtualKey).ToString();
        }

        return virtualKey is >= 112 and <= 135 ? $"F{virtualKey - 111}" : $"VK{virtualKey}";
    }

    /// <summary>Normalized, culture-independent text ("Ctrl+Shift+P"); used as lookup key and for storage.</summary>
    public override string ToString() => ToDisplayString(null);

    /// <summary>Text for display, with optionally localized modifier names ("Strg+Umschalt+P").</summary>
    public string ToDisplayString(Func<KeyModifiers, string>? modifierName)
    {
        var name = modifierName ?? (m => m.ToString());
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add(name(KeyModifiers.Ctrl));
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add(name(KeyModifiers.Shift));
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add(name(KeyModifiers.Alt));
        if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add(name(KeyModifiers.Win));
        parts.Add(KeyName(VirtualKey));
        var single = string.Join("+", parts);
        return Then is null ? single : $"{single}, {Then.ToDisplayString(modifierName)}";
    }

    private static bool TryParseSingle(string text, out KeyGesture gesture)
    {
        gesture = null!;
        var modifiers = KeyModifiers.None;
        int? key = null;
        foreach (var raw in SplitTokens(text))
        {
            var token = raw.Trim();
            switch (token.ToLowerInvariant())
            {
                case "ctrl" or "control" or "strg":
                    modifiers |= KeyModifiers.Ctrl;
                    continue;
                case "shift" or "umschalt":
                    modifiers |= KeyModifiers.Shift;
                    continue;
                case "alt":
                    modifiers |= KeyModifiers.Alt;
                    continue;
                case "win" or "windows":
                    modifiers |= KeyModifiers.Win;
                    continue;
            }

            if (key is not null || !TryParseKey(token, out var virtualKey))
            {
                return false;
            }

            key = virtualKey;
        }

        if (key is null)
        {
            return false;
        }

        gesture = new KeyGesture(modifiers, key.Value);
        return true;
    }

    private static List<string> SplitTokens(string text)
    {
        // "Ctrl++" must yield ["Ctrl", "+"], so a '+' only separates when something precedes it.
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (c == '+' && current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static bool TryParseKey(string token, out int virtualKey)
    {
        if (NamedKeys.TryGetValue(token, out virtualKey))
        {
            return true;
        }

        if (token.Length == 1 && char.IsAsciiLetterOrDigit(token[0]))
        {
            virtualKey = char.ToUpperInvariant(token[0]);
            return true;
        }

        if (token.Length is 2 or 3 && (token[0] is 'F' or 'f') && int.TryParse(token.AsSpan(1), out var number) && number is >= 1 and <= 24)
        {
            virtualKey = 111 + number;
            return true;
        }

        if (token.StartsWith("VK", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.AsSpan(2), out virtualKey))
        {
            return true;
        }

        virtualKey = 0;
        return false;
    }
}
