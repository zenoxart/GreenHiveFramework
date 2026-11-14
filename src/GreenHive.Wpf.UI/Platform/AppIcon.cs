using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>
/// The Green Hive app icon (honeycomb, <c>Assets/GreenHive.ico</c>). The exe carries it as its Win32 icon
/// (<c>ApplicationIcon</c>), so every window of the process shows it by default; this class makes it explicit
/// for the main windows and provides it as an image for the title area.
/// </summary>
public static class AppIcon
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/GreenHive.ico", UriKind.Absolute);
    private static ImageSource? _source;

    /// <summary>The icon as an image source (frozen, loaded once).</summary>
    public static ImageSource Source => _source ??= Load();

    /// <summary>Creates an image of the icon in the given size (device-independent pixels).</summary>
    public static Image CreateImage(double size) => new()
    {
        Source = Source,
        Width = size,
        Height = size,
        VerticalAlignment = VerticalAlignment.Center,
        SnapsToDevicePixels = true,
    };

    /// <summary>Sets the icon on a window.</summary>
    public static void Apply(Window window) => window.Icon = Source;

    private static ImageSource Load()
    {
        var decoder = new IconBitmapDecoder(IconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        frame.Freeze();
        return frame;
    }
}
