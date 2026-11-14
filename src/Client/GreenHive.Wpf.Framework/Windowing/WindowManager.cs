using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Windowing;

/// <summary>
/// Manages the application windows (WIFI: FensterManager): restores and saves their placement per monitor
/// in AppData, enforces minimum sizes, registers each window's dispatcher with the command pool and connects
/// its keyboard input to the shortcut manager. A window is restored on the monitor it was on; if that monitor is
/// gone, on a monitor with the same resolution or the primary one, always clamped into the work area.
/// </summary>
public sealed class WindowManager : AppObject
{
    private readonly Dictionary<string, Window> _windows = new(StringComparer.OrdinalIgnoreCase);
    private WindowPlacementStore? _placements;
    private ResponsiveLayoutManager? _responsive;
    private ShortcutInputRouter? _inputRouter;

    public event EventHandler<Window>? WindowClosed;

    public WindowPlacementStore Placements => _placements ??= AppContext.Produce<WindowPlacementStore>();

    public ResponsiveLayoutManager Responsive => _responsive ??= AppContext.Produce<ResponsiveLayoutManager>();

    /// <summary>Routes keyboard input of all managed windows to the shortcut manager.</summary>
    public ShortcutInputRouter InputRouter => _inputRouter ??= AppContext.Produce<ShortcutInputRouter>();

    public IReadOnlyDictionary<string, Window> Windows => _windows;

    /// <summary>
    /// Starts managing a window. Call before Show(): restores the saved placement, applies the minimum size and
    /// wires up dispatcher and shortcuts.
    /// </summary>
    public void Track(Window window, string key, int minWidth = 360, int minHeight = 480, bool isMain = false)
    {
        _windows[key] = window;
        window.MinWidth = Math.Max(window.MinWidth, minWidth);
        window.MinHeight = Math.Max(window.MinHeight, minHeight);

        var commands = (AppContext as ICommandContext)?.Commands;
        commands?.Dispatcher.Register(key, new WpfDispatcherQueue(window.Dispatcher), isMain);
        InputRouter.Attach(window);

        var saved = Placements.Get(key);
        if (saved is not null)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            {
                Restore(window, saved);
            }
            else
            {
                window.SourceInitialized += (_, _) => Restore(window, saved);
            }
        }

        window.LocationChanged += (_, _) => Capture(window, key);
        window.SizeChanged += (_, _) => Capture(window, key);
        window.StateChanged += (_, _) => Capture(window, key);
        window.Closing += (_, _) =>
        {
            Capture(window, key);
            Placements.Save();
        };
        window.Closed += (_, _) =>
        {
            _windows.Remove(key);
            commands?.Dispatcher.Unregister(key);
            InputRouter.Detach(window);
            WindowClosed?.Invoke(this, window);
        };

        AppContext.Log.Write($"Window '{key}' is managed", LogEntryType.Information, nameof(WindowManager));
    }

    private void Restore(Window window, WindowPlacement placement)
    {
        try
        {
            if (placement.Width <= 0 || placement.Height <= 0)
            {
                if (placement.Maximized)
                {
                    window.WindowState = WindowState.Maximized;
                }

                return;
            }

            var monitor = Native.FindMonitor(placement) ?? Native.PrimaryMonitor();
            var work = monitor.Work;
            var width = Math.Min(placement.Width, work.Width);
            var height = Math.Min(placement.Height, work.Height);
            var x = work.Left + Math.Clamp(placement.X, 0, Math.Max(0, work.Width - width));
            var y = work.Top + Math.Clamp(placement.Y, 0, Math.Max(0, work.Height - height));

            var handle = new WindowInteropHelper(window).Handle;
            var native = Native.GetPlacement(handle);
            // rcNormalPosition is in workspace coordinates of the primary monitor's work area.
            var primary = Native.PrimaryMonitor().Work;
            native.NormalPosition = new Native.Rect { Left = x - primary.Left, Top = y - primary.Top, Right = x - primary.Left + width, Bottom = y - primary.Top + height };
            native.ShowCmd = Native.SwShowNormal;
            Native.SetPlacement(handle, native);
            if (placement.Maximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex);
        }
    }

    private void Capture(Window window, string key)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || !window.IsVisible || window.WindowState == WindowState.Minimized)
            {
                return;
            }

            var native = Native.GetPlacement(handle);
            var monitor = Native.MonitorOf(handle);
            var primary = Native.PrimaryMonitor().Work;
            var placement = Placements.Get(key) ?? new WindowPlacement { Key = key };
            placement.Maximized = window.WindowState == WindowState.Maximized;
            placement.MonitorX = monitor.Bounds.Left;
            placement.MonitorY = monitor.Bounds.Top;
            placement.MonitorWidth = monitor.Bounds.Width;
            placement.MonitorHeight = monitor.Bounds.Height;
            var normal = native.NormalPosition;
            placement.X = normal.Left + primary.Left - monitor.Work.Left;
            placement.Y = normal.Top + primary.Top - monitor.Work.Top;
            placement.Width = normal.Width;
            placement.Height = normal.Height;
            Placements.Set(placement);
        }
        catch (Exception ex)
        {
            AppContext.Log.Write($"Could not capture the placement of '{key}': {ex.Message}", LogEntryType.Warning, nameof(WindowManager));
        }
    }

    /// <summary>Win32 monitor and placement functions (physical pixels).</summary>
    private static class Native
    {
        public const int SwShowNormal = 1;

        public static (Rect Bounds, Rect Work) PrimaryMonitor() => Info(MonitorFromPoint(new Point32(0, 0), MonitorDefaultToPrimary));

        public static (Rect Bounds, Rect Work) MonitorOf(IntPtr window) => Info(MonitorFromWindow(window, MonitorDefaultToNearest));

        public static (Rect Bounds, Rect Work)? FindMonitor(WindowPlacement placement)
        {
            var monitors = new List<(Rect Bounds, Rect Work)>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
            {
                monitors.Add(Info(monitor));
                return true;
            }, IntPtr.Zero);

            foreach (var monitor in monitors)
            {
                if (monitor.Bounds.Left == placement.MonitorX && monitor.Bounds.Top == placement.MonitorY &&
                    monitor.Bounds.Width == placement.MonitorWidth && monitor.Bounds.Height == placement.MonitorHeight)
                {
                    return monitor;
                }
            }

            // Same resolution elsewhere (monitors rearranged) is the next best match.
            foreach (var monitor in monitors)
            {
                if (monitor.Bounds.Width == placement.MonitorWidth && monitor.Bounds.Height == placement.MonitorHeight)
                {
                    return monitor;
                }
            }

            return null;
        }

        public static WindowPlacementNative GetPlacement(IntPtr window)
        {
            var placement = new WindowPlacementNative { Length = Marshal.SizeOf<WindowPlacementNative>() };
            GetWindowPlacement(window, ref placement);
            return placement;
        }

        public static void SetPlacement(IntPtr window, WindowPlacementNative placement)
        {
            placement.Length = Marshal.SizeOf<WindowPlacementNative>();
            SetWindowPlacement(window, ref placement);
        }

        private const int MonitorDefaultToPrimary = 1;
        private const int MonitorDefaultToNearest = 2;

        private static (Rect Bounds, Rect Work) Info(IntPtr monitor)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            GetMonitorInfo(monitor, ref info);
            return (info.Monitor, info.Work);
        }

        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point32 point, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr window, ref WindowPlacementNative placement);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr window, ref WindowPlacementNative placement);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public readonly int Width => Right - Left;

            public readonly int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point32(int x, int y)
        {
            public int X = x;
            public int Y = y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowPlacementNative
        {
            public int Length;
            public int Flags;
            public int ShowCmd;
            public Point32 MinPosition;
            public Point32 MaxPosition;
            public Rect NormalPosition;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public int Flags;
        }
    }
}
