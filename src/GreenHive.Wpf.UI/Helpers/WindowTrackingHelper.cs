using System.Windows;

namespace GreenHive.Wpf.UI.Helpers
{
    /// <summary>
    /// Helper-Klasse zum einfachen Tracking von Fenstern
    /// </summary>
    public static class WindowTrackingHelper
    {
        /// <summary>
        /// Attached Property um automatisches Window Tracking zu aktivieren
        /// </summary>
        public static readonly DependencyProperty EnableTrackingProperty =
            DependencyProperty.RegisterAttached(
                "EnableTracking",
                typeof(bool),
                typeof(WindowTrackingHelper),
                new PropertyMetadata(false, OnEnableTrackingChanged));

        /// <summary>
        /// Attached Property für die Window ID
        /// </summary>
        public static readonly DependencyProperty WindowIdProperty =
            DependencyProperty.RegisterAttached(
                "WindowId",
                typeof(string),
                typeof(WindowTrackingHelper),
                new PropertyMetadata(null));

        public static bool GetEnableTracking(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableTrackingProperty);
        }

        public static void SetEnableTracking(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableTrackingProperty, value);
        }

        public static string GetWindowId(DependencyObject obj)
        {
            return (string)obj.GetValue(WindowIdProperty);
        }

        public static void SetWindowId(DependencyObject obj, string value)
        {
            obj.SetValue(WindowIdProperty, value);
        }

        private static void OnEnableTrackingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && (bool)e.NewValue)
            {
                var windowId = GetWindowId(window) ?? window.GetType().Name;
                GreenHive.Wpf.UI.Common.WindowManager.Instance.TrackWindow(window, windowId);
            }
        }
    }
}

