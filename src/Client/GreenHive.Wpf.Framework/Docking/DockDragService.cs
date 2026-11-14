using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GreenHive.NET.ModuleContracts;

namespace GreenHive.Wpf.Framework.Docking;

/// <summary>
/// Drag and drop of tabs: dragging a tab header shows the dock guides; releasing over a guide docks the item there,
/// releasing outside the main window floats it at the pointer. Holding Ctrl suppresses docking (always float).
/// </summary>
internal static class DockDragService
{
    /// <summary>Screen position in device-independent pixels (what Window.Left/Top expect).</summary>
    public static Point ToScreenDips(Visual visual, Point position)
    {
        var screen = visual.PointToScreen(position);
        return PresentationSource.FromVisual(visual)?.CompositionTarget is { } target
            ? target.TransformFromDevice.Transform(screen)
            : screen;
    }

    public static void Attach(FrameworkElement header, DockManager manager, DockHost host, DockItem item)
    {
        Point? start = null;
        var dragging = false;
        DockSide? target = null;

        header.PreviewMouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(host);
            dragging = false;
        };

        header.PreviewMouseMove += (_, e) =>
        {
            if (start is not { } origin || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var position = e.GetPosition(host);
            if (!dragging)
            {
                if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance * 4 &&
                    Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance * 4)
                {
                    return;
                }

                dragging = true;
                header.CaptureMouse();
            }

            target = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? null : host.ShowGuides(position);
            e.Handled = true;
        };

        header.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!dragging)
            {
                start = null;
                return;
            }

            dragging = false;
            start = null;
            header.ReleaseMouseCapture();
            host.HideGuides();
            var position = e.GetPosition(host);
            var inside = position.X >= 0 && position.Y >= 0 && position.X <= host.ActualWidth && position.Y <= host.ActualHeight;
            if (target is { } side)
            {
                manager.MoveTo(item, side);
            }
            else if (!inside || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                manager.MoveTo(item, DockSide.Floating, ToScreenDips(host, position));
            }

            target = null;
            e.Handled = true;
        };

        header.LostMouseCapture += (_, _) =>
        {
            if (dragging)
            {
                dragging = false;
                start = null;
                host.HideGuides();
            }
        };
    }
}
