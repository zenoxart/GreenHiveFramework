using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Threading;
using Zenox.NET.Core.Common.Telemetry;

namespace Zenox.Wpf.Controls.Common.PerformanceMonitor
{
    /// <summary>
    /// Interaktionslogik fuer PerformancePanelView.xaml
    /// </summary>
    public partial class PerformancePanelView : UserControl
    {
        private readonly ObservableCollection<TelemetryEvent> _events;

        public PerformancePanelView()
        {
            InitializeComponent();

            _events = new ObservableCollection<TelemetryEvent>();
            EventsListBox.ItemsSource = _events;

            // Events abonnieren
            TelemetryService.Instance.EventLogged += OnEventLogged;

            // Bestehende Events laden
            LoadExistingEvents();
        }

        private void OnEventLogged(object? sender, TelemetryEvent e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _events.Insert(0, e);

                // Maximal 100 Events anzeigen
                while (_events.Count > 100)
                {
                    _events.RemoveAt(_events.Count - 1);
                }
            });
        }

        private void LoadExistingEvents()
        {
            foreach (var evt in TelemetryService.Instance.Events.TakeLast(100).Reverse())
            {
                _events.Add(evt);
            }
        }
    }
}
