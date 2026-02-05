using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Zenox.NET.Core.Common.Command;
using Zenox.NET.Core.Common.Heatmap;

namespace Zenox.Wpf.Controls.Common.Heatmap
{
    /// <summary>
    /// ViewModel fuer die Interaction Heatmap View
    /// </summary>
    public class InteractionHeatmapViewModel : INotifyPropertyChanged
    {
        private readonly InteractionHeatmapController _controller;
        private string _statusMessage = string.Empty;
        private HeatmapStatistics _statistics;
        private InteractionType? _selectedFilterType;
        private ObservableCollection<HeatmapCell> _heatmapCells;
        private ObservableCollection<ElementInteractionInfo> _topElements;
        private double _canvasWidth = 800;
        private double _canvasHeight = 600;

        public InteractionHeatmapViewModel()
        {
            _controller = InteractionHeatmapController.Instance;
            _statistics = new HeatmapStatistics();
            _heatmapCells = new ObservableCollection<HeatmapCell>();
            _topElements = new ObservableCollection<ElementInteractionInfo>();

            InitializeCommands();
            SubscribeToEvents();

            // Initial laden und Status vom Controller uebernehmen
            RefreshData();
            StatusMessage = _controller.IsTracking 
                ? $"Tracking aktiv - {_controller.InteractionCount} Interaktionen" 
                : "Tracking inaktiv";
        }

        #region Properties

        /// <summary>
        /// Tracking aktiv - synchronisiert mit Controller
        /// </summary>
        public bool IsTracking
        {
            get => _controller.IsTracking;
            set
            {
                if (_controller.IsTracking != value)
                {
                    if (value)
                    {
                        _controller.StartTracking();
                        StatusMessage = "Tracking gestartet";
                    }
                    else
                    {
                        _controller.StopTracking();
                        StatusMessage = "Tracking gestoppt";
                    }
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Status-Nachricht
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Aktuelle Statistiken
        /// </summary>
        public HeatmapStatistics Statistics
        {
            get => _statistics;
            set
            {
                _statistics = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Heatmap-Zellen
        /// </summary>
        public ObservableCollection<HeatmapCell> HeatmapCells
        {
            get => _heatmapCells;
            set
            {
                _heatmapCells = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Top-Elemente nach Interaktionen
        /// </summary>
        public ObservableCollection<ElementInteractionInfo> TopElements
        {
            get => _topElements;
            set
            {
                _topElements = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Canvas-Breite
        /// </summary>
        public double CanvasWidth
        {
            get => _canvasWidth;
            set
            {
                _canvasWidth = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Canvas-Hoehe
        /// </summary>
        public double CanvasHeight
        {
            get => _canvasHeight;
            set
            {
                _canvasHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Ausgewaehlter Filter-Typ
        /// </summary>
        public InteractionType? SelectedFilterType
        {
            get => _selectedFilterType;
            set
            {
                _selectedFilterType = value;
                OnPropertyChanged();
                RefreshHeatmap();
            }
        }

        /// <summary>
        /// Verfuegbare Filter-Typen
        /// </summary>
        public IReadOnlyList<InteractionType?> FilterTypes { get; } = new List<InteractionType?>
        {
            null, // Alle
            InteractionType.Click,
            InteractionType.DoubleClick,
            InteractionType.RightClick,
            InteractionType.Hover,
            InteractionType.Scroll
        };

        /// <summary>
        /// Konfiguration
        /// </summary>
        public HeatmapConfiguration Configuration => _controller.Configuration;

        /// <summary>
        /// Clicks tracken
        /// </summary>
        public bool TrackClicks
        {
            get => _controller.Configuration.TrackClicks;
            set
            {
                _controller.Configuration.TrackClicks = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Hover tracken
        /// </summary>
        public bool TrackHover
        {
            get => _controller.Configuration.TrackHover;
            set
            {
                _controller.Configuration.TrackHover = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Scroll tracken
        /// </summary>
        public bool TrackScroll
        {
            get => _controller.Configuration.TrackScroll;
            set
            {
                _controller.Configuration.TrackScroll = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Zellengroesse
        /// </summary>
        public int CellSize
        {
            get => _controller.Configuration.CellSize;
            set
            {
                _controller.Configuration.CellSize = Math.Max(5, Math.Min(100, value));
                OnPropertyChanged();
                RefreshHeatmap();
            }
        }

        #endregion

        #region Commands

        public ICommand StartTrackingCommand { get; private set; } = null!;
        public ICommand StopTrackingCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand ClearDataCommand { get; private set; } = null!;
        public ICommand ExportCommand { get; private set; } = null!;
        public ICommand NewSessionCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            StartTrackingCommand = new RelayCommand(
                _ =>
                {
                    IsTracking = true;
                },
                _ => !IsTracking)
            { Name = "StartTracking" };

            StopTrackingCommand = new RelayCommand(
                _ =>
                {
                    IsTracking = false;
                },
                _ => IsTracking)
            { Name = "StopTracking" };

            RefreshCommand = new RelayCommand(
                _ => 
                {
                    RefreshData();
                    StatusMessage = $"Aktualisiert - {_controller.InteractionCount} Interaktionen";
                })
            { Name = "RefreshHeatmap" };

            ClearDataCommand = new RelayCommand(
                _ =>
                {
                    _controller.ClearInteractions();
                    RefreshData();
                    StatusMessage = "Daten geloescht";
                })
            { Name = "ClearData" };

            ExportCommand = new RelayCommand(
                _ =>
                {
                    var json = _controller.ExportToJson();
                    System.Windows.Clipboard.SetText(json);
                    StatusMessage = "JSON in Zwischenablage kopiert";
                })
            { Name = "ExportHeatmap" };

            NewSessionCommand = new RelayCommand(
                _ =>
                {
                    _controller.StartNewSession();
                    RefreshData();
                    StatusMessage = "Neue Session gestartet";
                })
            { Name = "NewSession" };
        }

        #endregion

        #region Methods

        private void SubscribeToEvents()
        {
            _controller.InteractionRecorded += (s, e) =>
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    // Statistiken aktualisieren bei jeder Interaktion
                    Statistics = _controller.GetStatistics();
                    RefreshTopElements();
                    StatusMessage = $"Tracking aktiv - {_controller.InteractionCount} Interaktionen";
                });
            };

            _controller.HeatmapUpdated += (s, cells) =>
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    UpdateHeatmapCells(cells);
                });
            };
        }

        private void RefreshData()
        {
            Statistics = _controller.GetStatistics();
            RefreshHeatmap();
            RefreshTopElements();
            OnPropertyChanged(nameof(IsTracking));
        }

        private void RefreshHeatmap()
        {
            var cells = _controller.GenerateHeatmap(CanvasWidth, CanvasHeight, SelectedFilterType);
            UpdateHeatmapCells(cells);
        }

        private void UpdateHeatmapCells(IReadOnlyList<HeatmapCell> cells)
        {
            HeatmapCells.Clear();
            foreach (var cell in cells)
            {
                HeatmapCells.Add(cell);
            }
        }

        private void RefreshTopElements()
        {
            TopElements.Clear();
            var elements = _controller.GetTopElements(10);
            foreach (var element in elements)
            {
                TopElements.Add(element);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
