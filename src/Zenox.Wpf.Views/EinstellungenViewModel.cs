using System.Collections.ObjectModel;
using System.Windows.Input;
using Zenox.NET.Core.Common.Command;
using Zenox.NET.Core.Common.MVVM.ViewModelSupport;
using Zenox.NET.Core.Common.Settings;
using Zenox.NET.Core.Common.Snapshots;

namespace Zenox.Wpf.Views
{
    /// <summary>
    /// ViewModel für die Anwendungseinstellungen.
    /// </summary>
    public class EinstellungenViewModel : NotificationObject
    {
        private readonly SnapshotManager _snapshotManager;

        public EinstellungenViewModel()
        {
            _snapshotManager = SnapshotManager.Instance;
            AvailableSnapshots = new ObservableCollection<StateSnapshot>();
            InitializeCommands();
            LoadAvailableSnapshots();

            // Events abonnieren
            _snapshotManager.SnapshotCreated += (s, e) => LoadAvailableSnapshots();
            _snapshotManager.SnapshotDeleted += (s, e) => LoadAvailableSnapshots();
        }

        private void InitializeCommands()
        {
            SaveSettingsCommand = new RelayCommand(
                async _ => await SaveSettingsAsync())
            { Name = "SaveSettings" };

            ClearAllSnapshotsCommand = new RelayCommand(
                _ => ClearAllSnapshots(),
                _ => SnapshotCount > 0)
            { Name = "ClearAllSnapshots" };

            CreateManualSnapshotCommand = new RelayCommand(
                _ => CreateManualSnapshot())
            { Name = "CreateManualSnapshot" };

            LoadSnapshotCommand = new RelayCommand(
                _ => LoadSelectedSnapshot(),
                _ => SelectedSnapshot != null)
            { Name = "LoadSnapshot" };

            DeleteSnapshotCommand = new RelayCommand(
                _ => DeleteSelectedSnapshot(),
                _ => SelectedSnapshot != null)
            { Name = "DeleteSnapshot" };

            RefreshSnapshotsCommand = new RelayCommand(
                _ => LoadAvailableSnapshots())
            { Name = "RefreshSnapshots" };
        }

        #region Snapshot Settings Properties

        /// <summary>
        /// Auto-Snapshots aktiviert/deaktiviert.
        /// </summary>
        public bool AutoSnapshotsEnabled
        {
            get => _snapshotManager.Settings.AutoSnapshotsEnabled;
            set
            {
                if (_snapshotManager.Settings.AutoSnapshotsEnabled != value)
                {
                    _snapshotManager.SetAutoSnapshotEnabled(value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Auto-Snapshot-Intervall in Minuten.
        /// </summary>
        public int AutoSnapshotIntervalMinutes
        {
            get => _snapshotManager.Settings.AutoSnapshotIntervalMinutes;
            set
            {
                if (_snapshotManager.Settings.AutoSnapshotIntervalMinutes != value)
                {
                    _snapshotManager.SetAutoSnapshotInterval(value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Maximale Anzahl an Snapshots.
        /// </summary>
        public int MaxSnapshotCount
        {
            get => _snapshotManager.Settings.MaxSnapshotCount;
            set
            {
                if (_snapshotManager.Settings.MaxSnapshotCount != value)
                {
                    _snapshotManager.Settings.MaxSnapshotCount = Math.Max(1, value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Snapshot bei Start erstellen.
        /// </summary>
        public bool SnapshotOnStartup
        {
            get => _snapshotManager.Settings.SnapshotOnStartup;
            set
            {
                if (_snapshotManager.Settings.SnapshotOnStartup != value)
                {
                    _snapshotManager.Settings.SnapshotOnStartup = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Snapshot bei Beenden erstellen.
        /// </summary>
        public bool SnapshotOnShutdown
        {
            get => _snapshotManager.Settings.SnapshotOnShutdown;
            set
            {
                if (_snapshotManager.Settings.SnapshotOnShutdown != value)
                {
                    _snapshotManager.Settings.SnapshotOnShutdown = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Category Settings Properties

        /// <summary>
        /// Fenster-Layout speichern.
        /// </summary>
        public bool SnapshotWindowLayout
        {
            get => _snapshotManager.Settings.SnapshotCategories.WindowLayout;
            set
            {
                if (_snapshotManager.Settings.SnapshotCategories.WindowLayout != value)
                {
                    _snapshotManager.Settings.SnapshotCategories.WindowLayout = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Benutzereinstellungen speichern.
        /// </summary>
        public bool SnapshotUserSettings
        {
            get => _snapshotManager.Settings.SnapshotCategories.UserSettings;
            set
            {
                if (_snapshotManager.Settings.SnapshotCategories.UserSettings != value)
                {
                    _snapshotManager.Settings.SnapshotCategories.UserSettings = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Anwendungskonfiguration speichern.
        /// </summary>
        public bool SnapshotAppConfiguration
        {
            get => _snapshotManager.Settings.SnapshotCategories.AppConfiguration;
            set
            {
                if (_snapshotManager.Settings.SnapshotCategories.AppConfiguration != value)
                {
                    _snapshotManager.Settings.SnapshotCategories.AppConfiguration = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Session-Daten speichern.
        /// </summary>
        public bool SnapshotSessionData
        {
            get => _snapshotManager.Settings.SnapshotCategories.SessionData;
            set
            {
                if (_snapshotManager.Settings.SnapshotCategories.SessionData != value)
                {
                    _snapshotManager.Settings.SnapshotCategories.SessionData = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Benutzerdefinierte Daten speichern.
        /// </summary>
        public bool SnapshotCustomData
        {
            get => _snapshotManager.Settings.SnapshotCategories.CustomData;
            set
            {
                if (_snapshotManager.Settings.SnapshotCategories.CustomData != value)
                {
                    _snapshotManager.Settings.SnapshotCategories.CustomData = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Snapshot List Properties

        /// <summary>
        /// Liste der verfügbaren Snapshots.
        /// </summary>
        public ObservableCollection<StateSnapshot> AvailableSnapshots { get; }

        /// <summary>
        /// Ausgewählter Snapshot.
        /// </summary>
        private StateSnapshot? _selectedSnapshot;
        public StateSnapshot? SelectedSnapshot
        {
            get => _selectedSnapshot;
            set
            {
                if (SetProperty(ref _selectedSnapshot, value))
                {
                    OnPropertyChanged(nameof(SelectedSnapshotDetails));
                }
            }
        }

        /// <summary>
        /// Details zum ausgewählten Snapshot.
        /// </summary>
        public string SelectedSnapshotDetails
        {
            get
            {
                if (SelectedSnapshot == null)
                    return "Kein Snapshot ausgewählt";

                return $"Name: {SelectedSnapshot.Name}\n" +
                       $"Erstellt: {SelectedSnapshot.Timestamp.ToLocalTime():dd.MM.yyyy HH:mm:ss}\n" +
                       $"Typ: {SelectedSnapshot.Type}\n" +
                       $"Kategorie: {SelectedSnapshot.Category}\n" +
                       $"Größe: {FormatBytes(SelectedSnapshot.SizeBytes)}\n" +
                       $"Version: {SelectedSnapshot.AppVersion}";
            }
        }

        #endregion

        #region Info Properties

        /// <summary>
        /// Anzahl der gespeicherten Snapshots.
        /// </summary>
        public int SnapshotCount => _snapshotManager.GetSnapshots().Count;

        /// <summary>
        /// Gesamtgröße der Snapshots.
        /// </summary>
        public string TotalSnapshotSize
        {
            get
            {
                var bytes = _snapshotManager.GetTotalSize();
                return FormatBytes(bytes);
            }
        }

        /// <summary>
        /// Status-Nachricht.
        /// </summary>
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region Commands

        public ICommand SaveSettingsCommand { get; private set; } = null!;
        public ICommand ClearAllSnapshotsCommand { get; private set; } = null!;
        public ICommand CreateManualSnapshotCommand { get; private set; } = null!;
        public ICommand LoadSnapshotCommand { get; private set; } = null!;
        public ICommand DeleteSnapshotCommand { get; private set; } = null!;
        public ICommand RefreshSnapshotsCommand { get; private set; } = null!;

        #endregion

        #region Methods

        private async Task SaveSettingsAsync()
        {
            await _snapshotManager.SaveSettingsAsync();
            StatusMessage = "? Einstellungen gespeichert";
            RefreshInfo();
        }

        private void ClearAllSnapshots()
        {
            _snapshotManager.ClearAll();
            StatusMessage = "? Alle Snapshots gelöscht";
            RefreshInfo();
            LoadAvailableSnapshots();
        }

        private void CreateManualSnapshot()
        {
            _snapshotManager.CreateAllCategorySnapshots("Manuell");
            StatusMessage = "? Manueller Snapshot erstellt";
            RefreshInfo();
            LoadAvailableSnapshots();
        }

        private void LoadAvailableSnapshots()
        {
            AvailableSnapshots.Clear();
            var snapshots = _snapshotManager.GetSnapshots();
            foreach (var snapshot in snapshots)
            {
                AvailableSnapshots.Add(snapshot);
            }
            RefreshInfo();
        }

        private void LoadSelectedSnapshot()
        {
            if (SelectedSnapshot == null) return;

            try
            {
                // Versuche den Snapshot wiederherzustellen
                var success = _snapshotManager.RestoreCategorySnapshot(SelectedSnapshot.Id);
                
                if (success)
                {
                    StatusMessage = $"? Snapshot '{SelectedSnapshot.Name}' geladen";
                }
                else
                {
                    // Fallback: Zeige die Snapshot-Daten
                    var data = SelectedSnapshot.RestoreDynamic();
                    StatusMessage = $"? Snapshot-Daten geladen (kein Handler registriert)";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"? Fehler beim Laden: {ex.Message}";
            }
        }

        private void DeleteSelectedSnapshot()
        {
            if (SelectedSnapshot == null) return;

            var snapshotName = SelectedSnapshot.Name;
            var success = _snapshotManager.DeleteSnapshot(SelectedSnapshot.Id);

            if (success)
            {
                StatusMessage = $"? Snapshot '{snapshotName}' gelöscht";
                SelectedSnapshot = null;
                LoadAvailableSnapshots();
            }
            else
            {
                StatusMessage = $"? Snapshot konnte nicht gelöscht werden";
            }
        }

        private void RefreshInfo()
        {
            OnPropertyChanged(nameof(SnapshotCount));
            OnPropertyChanged(nameof(TotalSnapshotSize));
        }

        private static string FormatBytes(long bytes)
        {
            return bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:F2} KB",
                _ => $"{bytes / 1024.0 / 1024.0:F2} MB"
            };
        }

        #endregion
    }
}
