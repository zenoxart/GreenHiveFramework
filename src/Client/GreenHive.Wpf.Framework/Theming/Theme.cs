using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GreenHive.Wpf.Framework.Theming;

/// <summary>
/// Theme resources for views built in code. Brushes are looked up by the resource keys of the WPF Fluent theme
/// (PresentationFramework.Fluent) and bound dynamically, so they follow light/dark/high-contrast; when a key is
/// missing (other themes) a neutral fallback is used instead.
/// </summary>
public static class Theme
{
    public const string TextPrimary = "TextFillColorPrimaryBrush";
    public const string TextSecondary = "TextFillColorSecondaryBrush";
    public const string CardBackground = "CardBackgroundFillColorDefaultBrush";
    public const string CardStroke = "CardStrokeColorDefaultBrush";
    public const string LayerBackground = "LayerFillColorDefaultBrush";
    public const string SubtleFill = "SubtleFillColorSecondaryBrush";
    public const string Divider = "DividerStrokeColorDefaultBrush";
    public const string Accent = "AccentFillColorDefaultBrush";
    public const string AccentText = "TextOnAccentFillColorPrimaryBrush";
    public const string SolidBackground = "SolidBackgroundFillColorBaseBrush";

    /// <summary>Green accent of Green Hive (used where the theme has no accent).</summary>
    public static readonly Color Brand = Color.FromRgb(0x2F, 0xA6, 0x5A);

    /// <summary>Icon font: Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10.</summary>
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private static readonly Dictionary<string, Brush> Fallbacks = new(StringComparer.Ordinal)
    {
        [TextPrimary] = Frozen(Color.FromRgb(0x1B, 0x1B, 0x1B)),
        [TextSecondary] = Frozen(Color.FromRgb(0x5F, 0x5F, 0x5F)),
        [CardBackground] = Frozen(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF)),
        [CardStroke] = Frozen(Color.FromArgb(0x24, 0x00, 0x00, 0x00)),
        [LayerBackground] = Frozen(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
        [SubtleFill] = Frozen(Color.FromArgb(0x09, 0x00, 0x00, 0x00)),
        [Divider] = Frozen(Color.FromArgb(0x14, 0x00, 0x00, 0x00)),
        [Accent] = Frozen(Brand),
        [AccentText] = Frozen(Colors.White),
        [SolidBackground] = Frozen(Color.FromRgb(0xF3, 0xF3, 0xF3)),
    };

    /// <summary>Severity colours of messages and badges.</summary>
    public static Brush Success { get; } = Frozen(Color.FromRgb(0x0F, 0x7B, 0x0F));

    public static Brush Warning { get; } = Frozen(Color.FromRgb(0x9D, 0x5D, 0x00));

    public static Brush Error { get; } = Frozen(Color.FromRgb(0xC4, 0x2B, 0x1C));

    public static Brush Info { get; } = Frozen(Color.FromRgb(0x00, 0x5F, 0xB8));

    public static Brush BrandBrush { get; } = Frozen(Brand);

    /// <summary>Binds a brush property to a theme resource (dynamic) or sets the fallback when the theme lacks it.</summary>
    public static T Brush<T>(this T element, DependencyProperty property, string key)
        where T : FrameworkElement
    {
        if (Find(key) is not null)
        {
            element.SetResourceReference(property, key);
        }
        else
        {
            element.SetValue(property, Fallbacks.TryGetValue(key, out var fallback) ? fallback : Brushes.Transparent);
        }

        return element;
    }

    /// <summary>Resolves a brush now (for drawing code that cannot bind).</summary>
    public static Brush Resolve(string key) =>
        Find(key) as Brush ?? (Fallbacks.TryGetValue(key, out var fallback) ? fallback : Brushes.Transparent);

    /// <summary>Applies a named style from the theme when it exists (e.g. "AccentButtonStyle").</summary>
    public static T Styled<T>(this T element, string styleKey)
        where T : FrameworkElement
    {
        if (Find(styleKey) is Style style && style.TargetType.IsAssignableFrom(typeof(T)))
        {
            element.Style = style;
        }

        return element;
    }

    /// <summary>An icon glyph as text element.</summary>
    public static TextBlock Glyph(string glyph, double size = 16) => new()
    {
        Text = glyph,
        FontFamily = IconFont,
        FontSize = size,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    /// <summary>Text styles (sizes of the Fluent type ramp).</summary>
    public static TextBlock Title(this TextBlock text)
    {
        text.FontSize = 20;
        text.FontWeight = FontWeights.SemiBold;
        return text;
    }

    public static TextBlock Caption(this TextBlock text)
    {
        text.FontSize = 12;
        return text.Brush(TextBlock.ForegroundProperty, TextSecondary);
    }

    public static TextBlock Strong(this TextBlock text)
    {
        text.FontWeight = FontWeights.SemiBold;
        return text;
    }

    private static object? Find(string key) => Application.Current?.TryFindResource(key);

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
