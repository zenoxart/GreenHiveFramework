using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Zenox.Wpf.Core.Common.Command;
using Zenox.Wpf.Core.Common.MVVM.ViewModelSupport;
using Zenox.Wpf.Core.Common.Snapshots;

namespace Zenox.Wpf.Views;

/// <summary>
/// ViewModel für die SnapshotDemoView.
/// Demonstriert die Verwendung des SnapshotManager-Frameworks.
/// </summary>
public class SnapshotDemoViewModel : NotificationObject
{
    private string _snapshotName = string.Empty;
    private string _stateData = "{\n  \"CurrentUser\": \"Demo User\",\n  \"Theme\": \"Dark\",\n  \"Settings\": {\n    \"AutoSave\": true,\n    \"Language\": \"de-DE\"\n  }\n}";
    private SnapshotType _selectedType = SnapshotType.Manual;
    private StateSnapshot? _selectedSnapshot;
    private string _statusMessage = string.Empty;
    private string _restoredData = string.Empty;
    private string _totalSizeInfo = string.Empty;

    public SnapshotDemoViewModel()
    {
        Snapshots = new ObservableCollection<StateSnapshot>();
        SnapshotTypes = new ObservableCollection<SnapshotType>(Enum.GetValues<SnapshotType>());

        InitializeCommands();
        LoadSnapshots();
        SubscribeToEvents();
    }

    private void InitializeCommands()
    {
        CreateSnapshotCommand = new RelayCommand(
            _ => CreateSnapshot(),
            _ => !string.IsNullOrWhiteSpace(SnapshotName) && !string.IsNullOrWhiteSpace(StateData))
        { Name = "CreateSnapshot" };

        RestoreSnapshotCommand = new RelayCommand(
            _ => RestoreSnapshot(),
            _ => SelectedSnapshot != null)
        { Name = "RestoreSnapshot" };

        DeleteSnapshotCommand = new RelayCommand(
            _ => DeleteSnapshot(),
            _ => SelectedSnapshot != null)
        { Name = "DeleteSnapshot" };

        ClearAllSnapshotsCommand = new RelayCommand(
            _ => ClearAllSnapshots(),
            _ => Snapshots.Count > 0)
        { Name = "ClearAllSnapshots" };

        RefreshSnapshotsCommand = new RelayCommand(
            _ => LoadSnapshots())
        { Name = "RefreshSnapshots" };

        LoadExampleStateCommand = new RelayCommand(
            param => LoadExampleState(param?.ToString() ?? "simple"))
        { Name = "LoadExampleState" };
    }

    private void SubscribeToEvents()
    {
        SnapshotManager.Instance.SnapshotCreated += (s, snapshot) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                LoadSnapshots();
                StatusMessage = $"? Snapshot '{snapshot.Name}' erstellt";
            });
        };

        SnapshotManager.Instance.SnapshotRestored += (s, snapshot) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                StatusMessage = $"? Snapshot '{snapshot.Name}' wiederhergestellt";
            });
        };

        SnapshotManager.Instance.SnapshotDeleted += (s, id) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                LoadSnapshots();
                StatusMessage = $"? Snapshot gelöscht";
            });
        };
    }

    #region Properties

    /// <summary>
    /// Name für den neuen Snapshot
    /// </summary>
    public string SnapshotName
    {
        get => _snapshotName;
        set => SetProperty(ref _snapshotName, value);
    }

    /// <summary>
    /// Zustandsdaten als JSON
    /// </summary>
    public string StateData
    {
        get => _stateData;
        set => SetProperty(ref _stateData, value);
    }

    /// <summary>
    /// Ausgewählter Snapshot-Typ
    /// </summary>
    public SnapshotType SelectedType
    {
        get => _selectedType;
        set => SetProperty(ref _selectedType, value);
    }

    /// <summary>
    /// Aktuell ausgewählter Snapshot
    /// </summary>
    public StateSnapshot? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            if (SetProperty(ref _selectedSnapshot, value))
            {
                UpdateRestoredData();
            }
        }
    }

    /// <summary>
    /// Statusnachricht für Benutzer-Feedback
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Wiederhergestellte Daten des ausgewählten Snapshots
    /// </summary>
    public string RestoredData
    {
        get => _restoredData;
        set => SetProperty(ref _restoredData, value);
    }

    /// <summary>
    /// Information über Gesamtgröße
    /// </summary>
    public string TotalSizeInfo
    {
        get => _totalSizeInfo;
        set => SetProperty(ref _totalSizeInfo, value);
    }

    /// <summary>
    /// Liste aller Snapshots
    /// </summary>
    public ObservableCollection<StateSnapshot> Snapshots { get; }

    /// <summary>
    /// Verfügbare Snapshot-Typen
    /// </summary>
    public ObservableCollection<SnapshotType> SnapshotTypes { get; }

    #endregion

    #region Commands

    public ICommand CreateSnapshotCommand { get; private set; } = null!;
    public ICommand RestoreSnapshotCommand { get; private set; } = null!;
    public ICommand DeleteSnapshotCommand { get; private set; } = null!;
    public ICommand ClearAllSnapshotsCommand { get; private set; } = null!;
    public ICommand RefreshSnapshotsCommand { get; private set; } = null!;
    public ICommand LoadExampleStateCommand { get; private set; } = null!;

    #endregion

    #region Methods

    private void LoadSnapshots()
    {
        Snapshots.Clear();
        var snapshots = SnapshotManager.Instance.GetSnapshots();
        foreach (var snapshot in snapshots)
        {
            Snapshots.Add(snapshot);
        }

        var totalBytes = SnapshotManager.Instance.GetTotalSize();
        TotalSizeInfo = $"{Snapshots.Count} Snapshots | {FormatBytes(totalBytes)}";
    }

    private void CreateSnapshot()
    {
        try
        {
            // Parse JSON to validate it
            var state = System.Text.Json.JsonSerializer.Deserialize<object>(StateData);
            
            SnapshotManager.Instance.CreateSnapshot(
                state,
                SnapshotName,
                SelectedType);

            // Reset input fields
            SnapshotName = string.Empty;
        }
        catch (System.Text.Json.JsonException ex)
        {
            StatusMessage = $"? Ungültiges JSON: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"? Fehler: {ex.Message}";
        }
    }

    private void RestoreSnapshot()
    {
        if (SelectedSnapshot == null) return;

        try
        {
            var state = SnapshotManager.Instance.RestoreSnapshot<object>(SelectedSnapshot.Id);
            RestoredData = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"? Fehler beim Wiederherstellen: {ex.Message}";
        }
    }

    private void DeleteSnapshot()
    {
        if (SelectedSnapshot == null) return;

        var result = SnapshotManager.Instance.DeleteSnapshot(SelectedSnapshot.Id);
        if (!result)
        {
            StatusMessage = "? Snapshot konnte nicht gelöscht werden";
        }
    }

    private void ClearAllSnapshots()
    {
        SnapshotManager.Instance.ClearAll();
        LoadSnapshots();
        StatusMessage = "? Alle Snapshots gelöscht";
        RestoredData = string.Empty;
    }

    private void UpdateRestoredData()
    {
        if (SelectedSnapshot == null)
        {
            RestoredData = string.Empty;
            return;
        }

        RestoredData = SelectedSnapshot.StateJson ?? string.Empty;
    }

    private void LoadExampleState(string exampleType)
    {
        StateData = exampleType switch
        {
            "user" => """
                {
                  "UserId": 12345,
                  "Username": "max.mustermann",
                  "Email": "max@example.com",
                  "Roles": ["Admin", "User"],
                  "LastLogin": "2024-01-15T10:30:00Z"
                }
                """,
            "settings" => """
                {
                  "Theme": "Dark",
                  "Language": "de-DE",
                  "AutoSave": true,
                  "AutoSaveInterval": 300,
                  "Notifications": {
                    "Email": true,
                    "Push": false,
                    "Sound": true
                  }
                }
                """,
            "window" => """
                {
                  "WindowState": "Normal",
                  "Width": 1280,
                  "Height": 720,
                  "Left": 100,
                  "Top": 50,
                  "IsMaximized": false,
                  "ActiveTab": "Dashboard"
                }
                """,
            "complex" => """
                {
                  "Application": {
                    "Name": "Zenox WPF Demo",
                    "Version": "1.0.0",
                    "BuildDate": "2024-01-15"
                  },
                  "Session": {
                    "Id": "abc123-def456",
                    "StartTime": "2024-01-15T08:00:00Z",
                    "User": {
                      "Id": 1,
                      "Name": "Demo User"
                    }
                  },
                  "Data": {
                    "OpenDocuments": ["doc1.txt", "doc2.txt"],
                    "RecentFiles": 5,
                    "UnsavedChanges": false
                  }
                }
                """,
            _ => """
                {
                  "Message": "Hello, Snapshot!",
                  "Timestamp": "2024-01-15T12:00:00Z"
                }
                """
        };

        SnapshotName = exampleType switch
        {
            "user" => "Benutzer-State",
            "settings" => "Einstellungen",
            "window" => "Fenster-Layout",
            "complex" => "Komplexer State",
            _ => "Einfacher State"
        };
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

/// <summary>
/// Konvertiert null zu Visibility.Collapsed und nicht-null zu Visibility.Visible
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
