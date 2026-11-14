using System.Collections.Concurrent;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.XamlMetadata;

/// <summary>
/// Resolves icon references (IconRef) to glyphs or images (WPF elements are created by IconFactory). References:
/// "save" (shell catalogue), "documents.invoice" (module icon), "glyph:E74E" (direct glyph),
/// "image:Assets/x.png" (file next to the host). Glyphs use the system symbol font
/// (Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10) and follow the
/// control's foreground, so they adapt to theme, hover, disabled and high contrast.
/// </summary>
public sealed class IconCatalog : AppObject
{
    private const string PlaceholderGlyph = "\uE946";

    private static readonly Dictionary<string, string> DefaultGlyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["add"] = "\uE710",
        ["apps"] = "\uE71D",
        ["attach"] = "\uE73F",
        ["back"] = "\uE72B",
        ["clear"] = "\uE894",
        ["close"] = "\uE711",
        ["delete"] = "\uE74D",
        ["detach"] = "\uE8A7",
        ["document"] = "\uE8A5",
        ["dock.bottom"] = "\uE90E",
        ["dock.left"] = "\uE90C",
        ["dock.right"] = "\uE90D",
        ["edit"] = "\uE70F",
        ["error"] = "\uE783",
        ["folder"] = "\uE8B7",
        ["forward"] = "\uE72A",
        ["fullscreen"] = "\uE740",
        ["help"] = "\uE897",
        ["home"] = "\uE80F",
        ["info"] = "\uE946",
        ["keyboard"] = "\uE765",
        ["language"] = "\uE774",
        ["library"] = "\uE8F1",
        ["list"] = "\uE8FD",
        ["more"] = "\uE712",
        ["output"] = "\uE943",
        ["pin"] = "\uE718",
        ["power"] = "\uE7E8",
        ["refresh"] = "\uE72C",
        ["reset"] = "\uE72C",
        ["save"] = "\uE74E",
        ["search"] = "\uE721",
        ["settings"] = "\uE713",
        ["switch"] = "\uE8AB",
        ["unpin"] = "\uE77A",
        ["view"] = "\uE890",
        ["warning"] = "\uE7BA",
    };

    private readonly ConcurrentDictionary<string, string> _glyphs = new(DefaultGlyphs, StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Uri> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers or replaces a glyph icon, e.g. RegisterGlyph("documents.invoice", "\uE8A5").</summary>
    public void RegisterGlyph(string name, string glyph) => _glyphs[name] = glyph;

    /// <summary>Registers an image icon (SVG or PNG), e.g. a PNG file in the module folder.</summary>
    public void RegisterImage(string name, Uri image) => _images[name] = image;

    /// <summary>Removes all icons whose name starts with the prefix (e.g. a module ID when it unloads).</summary>
    public void UnregisterPrefix(string prefix)
    {
        foreach (var key in _glyphs.Keys.Where(k => k.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)))
        {
            _glyphs.TryRemove(key, out _);
        }

        foreach (var key in _images.Keys.Where(k => k.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)))
        {
            _images.TryRemove(key, out _);
        }
    }

    /// <summary>Returns the glyph for a reference, or a neutral placeholder when it is unknown.</summary>
    public string ResolveGlyph(string? iconRef)
    {
        if (string.IsNullOrWhiteSpace(iconRef))
        {
            return PlaceholderGlyph;
        }

        if (iconRef.StartsWith("glyph:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return char.ConvertFromUtf32(Convert.ToInt32(iconRef[6..], 16));
            }
            catch (Exception)
            {
                return PlaceholderGlyph;
            }
        }

        if (_glyphs.TryGetValue(iconRef, out var glyph))
        {
            return glyph;
        }

        if (_reportedMissing.TryAdd(iconRef, 0) && HasAppContext)
        {
            AppContext.Log.Write($"Unknown icon '{iconRef}'", LogEntryType.Warning, nameof(IconCatalog));
        }

        return PlaceholderGlyph;
    }

    /// <summary>Resolves an image icon ("image:..." or a registered image).</summary>
    public bool TryGetImage(string? iconRef, out Uri image)
    {
        image = null!;
        if (string.IsNullOrWhiteSpace(iconRef))
        {
            return false;
        }

        if (iconRef.StartsWith("image:", StringComparison.OrdinalIgnoreCase))
        {
            image = new Uri(Path.Combine(System.AppContext.BaseDirectory, iconRef[6..].TrimStart('/')));
            return true;
        }

        if (_images.TryGetValue(iconRef, out var registered))
        {
            image = registered;
            return true;
        }

        return false;
    }
}
