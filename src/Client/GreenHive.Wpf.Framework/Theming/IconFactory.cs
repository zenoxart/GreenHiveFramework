using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GreenHive.Wpf.Framework.XamlMetadata;

namespace GreenHive.Wpf.Framework.Theming;

/// <summary>Creates WPF elements for icon references resolved by the <see cref="IconCatalog"/>.</summary>
public static class IconFactory
{
    /// <summary>Creates an icon element (for buttons, menu items, tabs). Glyphs follow the foreground of their parent.</summary>
    public static FrameworkElement CreateIcon(this IconCatalog catalog, string? iconRef, double size = 16)
    {
        if (catalog.TryGetImage(iconRef, out var image))
        {
            try
            {
                return new Image { Source = new BitmapImage(image), Width = size, Height = size, Stretch = Stretch.Uniform };
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
            {
                // fall through to the glyph placeholder
            }
        }

        return Theme.Glyph(catalog.ResolveGlyph(iconRef), size);
    }
}
