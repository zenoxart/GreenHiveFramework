namespace GreenHive.NET.Contracts;

public enum IconKind
{
    /// <summary>Symbolic name from the icon catalogue ("save", "documents.invoice").</summary>
    Catalog,

    /// <summary>Direct Segoe Fluent Icons glyph ("glyph:E74E").</summary>
    Glyph,

    /// <summary>Image file inside the module package ("image:Assets/invoice.svg").</summary>
    Image,
}

/// <summary>
/// Refers to an icon by a symbolic name instead of a code point or a file path.
/// No sizes or colours: the control decides the size, the theme the colour.
/// </summary>
public readonly record struct IconRef(string Value)
{
    public IconKind Kind => Value.StartsWith("glyph:", StringComparison.OrdinalIgnoreCase)
        ? IconKind.Glyph
        : Value.StartsWith("image:", StringComparison.OrdinalIgnoreCase) ? IconKind.Image : IconKind.Catalog;

    /// <summary>The part after the prefix ("E74E", "Assets/invoice.svg") or the catalogue name.</summary>
    public string Name => Kind == IconKind.Catalog ? Value : Value[6..];

    public static IconRef Glyph(string hex) => new($"glyph:{hex}");

    public override string ToString() => Value;

    public static implicit operator string(IconRef icon) => icon.Value;

    public static implicit operator IconRef(string value) => new(value);
}
