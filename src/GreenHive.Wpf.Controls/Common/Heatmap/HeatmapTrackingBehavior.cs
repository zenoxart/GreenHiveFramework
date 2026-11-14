using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using GreenHive.NET.Core.Common.Heatmap;

namespace GreenHive.Wpf.Controls.Common.Heatmap
{
    /// <summary>
    /// Attached Behavior fuer automatisches Interaction-Tracking auf UIElements
    /// </summary>
    public static class HeatmapTrackingBehavior
    {
        #region Attached Properties

        /// <summary>
        /// Aktiviert Heatmap-Tracking fuer das Element
        /// </summary>
        public static readonly DependencyProperty IsTrackingEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsTrackingEnabled",
                typeof(bool),
                typeof(HeatmapTrackingBehavior),
                new PropertyMetadata(false, OnIsTrackingEnabledChanged));

        /// <summary>
        /// Name der View fuer die Zuordnung
        /// </summary>
        public static readonly DependencyProperty ViewNameProperty =
            DependencyProperty.RegisterAttached(
                "ViewName",
                typeof(string),
                typeof(HeatmapTrackingBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Tracking von Klicks aktiviert
        /// </summary>
        public static readonly DependencyProperty TrackClicksProperty =
            DependencyProperty.RegisterAttached(
                "TrackClicks",
                typeof(bool),
                typeof(HeatmapTrackingBehavior),
                new PropertyMetadata(true));

        /// <summary>
        /// Tracking von Hover aktiviert
        /// </summary>
        public static readonly DependencyProperty TrackHoverProperty =
            DependencyProperty.RegisterAttached(
                "TrackHover",
                typeof(bool),
                typeof(HeatmapTrackingBehavior),
                new PropertyMetadata(true));

        /// <summary>
        /// Tracking von Scroll aktiviert
        /// </summary>
        public static readonly DependencyProperty TrackScrollProperty =
            DependencyProperty.RegisterAttached(
                "TrackScroll",
                typeof(bool),
                typeof(HeatmapTrackingBehavior),
                new PropertyMetadata(false));

        #endregion

        #region Getters and Setters

        public static bool GetIsTrackingEnabled(DependencyObject obj)
            => (bool)obj.GetValue(IsTrackingEnabledProperty);

        public static void SetIsTrackingEnabled(DependencyObject obj, bool value)
            => obj.SetValue(IsTrackingEnabledProperty, value);

        public static string GetViewName(DependencyObject obj)
            => (string)obj.GetValue(ViewNameProperty);

        public static void SetViewName(DependencyObject obj, string value)
            => obj.SetValue(ViewNameProperty, value);

        public static bool GetTrackClicks(DependencyObject obj)
            => (bool)obj.GetValue(TrackClicksProperty);

        public static void SetTrackClicks(DependencyObject obj, bool value)
            => obj.SetValue(TrackClicksProperty, value);

        public static bool GetTrackHover(DependencyObject obj)
            => (bool)obj.GetValue(TrackHoverProperty);

        public static void SetTrackHover(DependencyObject obj, bool value)
            => obj.SetValue(TrackHoverProperty, value);

        public static bool GetTrackScroll(DependencyObject obj)
            => (bool)obj.GetValue(TrackScrollProperty);

        public static void SetTrackScroll(DependencyObject obj, bool value)
            => obj.SetValue(TrackScrollProperty, value);

        #endregion

        #region Private Fields

        private static readonly Dictionary<UIElement, HoverTracker> _hoverTrackers = new();

        #endregion

        #region Event Handlers

        private static void OnIsTrackingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            if ((bool)e.NewValue)
            {
                AttachHandlers(element);
            }
            else
            {
                DetachHandlers(element);
            }
        }

        private static void AttachHandlers(UIElement element)
        {
            element.PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
            element.PreviewMouseRightButtonDown += OnMouseRightButtonDown;
            element.MouseEnter += OnMouseEnter;
            element.MouseLeave += OnMouseLeave;
            element.PreviewMouseWheel += OnMouseWheel;
        }

        private static void DetachHandlers(UIElement element)
        {
            element.PreviewMouseLeftButtonDown -= OnMouseLeftButtonDown;
            element.PreviewMouseRightButtonDown -= OnMouseRightButtonDown;
            element.MouseEnter -= OnMouseEnter;
            element.MouseLeave -= OnMouseLeave;
            element.PreviewMouseWheel -= OnMouseWheel;

            if (_hoverTrackers.TryGetValue(element, out var tracker))
            {
                tracker.Dispose();
                _hoverTrackers.Remove(element);
            }
        }

        private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not UIElement element) return;
            if (!GetTrackClicks(element)) return;

            var position = e.GetPosition(element);
            var targetInfo = GetTargetInfo(e.OriginalSource as DependencyObject);
            var viewName = GetViewName(element);

            InteractionHeatmapController.Instance.RecordClick(
                position.X,
                position.Y,
                targetInfo.Name,
                targetInfo.Type,
                viewName);
        }

        private static void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not UIElement element) return;
            if (!GetTrackClicks(element)) return;

            var position = e.GetPosition(element);
            var targetInfo = GetTargetInfo(e.OriginalSource as DependencyObject);
            var viewName = GetViewName(element);

            InteractionHeatmapController.Instance.RecordInteraction(
                position.X,
                position.Y,
                InteractionType.RightClick,
                targetInfo.Name,
                targetInfo.Type,
                viewName);
        }

        private static void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not UIElement element) return;
            if (!GetTrackHover(element)) return;

            var tracker = new HoverTracker(element, e.GetPosition(element));
            _hoverTrackers[element] = tracker;
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not UIElement element) return;
            if (!_hoverTrackers.TryGetValue(element, out var tracker)) return;

            var duration = tracker.GetDuration();
            var viewName = GetViewName(element);

            InteractionHeatmapController.Instance.RecordHover(
                tracker.Position.X,
                tracker.Position.Y,
                (long)duration.TotalMilliseconds,
                element.GetType().Name,
                element.GetType().FullName,
                viewName);

            tracker.Dispose();
            _hoverTrackers.Remove(element);
        }

        private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not UIElement element) return;
            if (!GetTrackScroll(element)) return;

            var position = e.GetPosition(element);
            var viewName = GetViewName(element);

            InteractionHeatmapController.Instance.RecordInteraction(
                position.X,
                position.Y,
                InteractionType.Scroll,
                viewName: viewName,
                metadata: new Dictionary<string, object> { ["Delta"] = e.Delta });
        }

        #endregion

        #region Helper Methods

        private static (string? Name, string? Type) GetTargetInfo(DependencyObject? target)
        {
            if (target == null) return (null, null);

            string? name = null;
            if (target is FrameworkElement fe)
            {
                name = !string.IsNullOrEmpty(fe.Name) ? fe.Name : fe.GetType().Name;
            }

            return (name, target.GetType().Name);
        }

        #endregion

        #region HoverTracker

        private class HoverTracker : IDisposable
        {
            private readonly Stopwatch _stopwatch;

            public UIElement Element { get; }
            public System.Windows.Point Position { get; }

            public HoverTracker(UIElement element, System.Windows.Point position)
            {
                Element = element;
                Position = position;
                _stopwatch = Stopwatch.StartNew();
            }

            public TimeSpan GetDuration()
            {
                _stopwatch.Stop();
                return _stopwatch.Elapsed;
            }

            public void Dispose()
            {
                _stopwatch.Stop();
            }
        }

        #endregion
    }
}
