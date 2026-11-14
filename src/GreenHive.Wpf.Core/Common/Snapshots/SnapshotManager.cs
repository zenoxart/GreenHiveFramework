using System.IO;
using System.Text.Json;
using System.Timers;
using GreenHive.NET.Core.Common.Services;
using GreenHive.NET.Core.Common.Settings;
using GreenHive.NET.Core.Common.Telemetry;

namespace GreenHive.NET.Core.Common.Snapshots
{
    /// <summary>
    /// Generischer Snapshot Manager für Anwendungszustand, Konfigurationen und Daten.
    /// Unterstützt kategorisierte Snapshots und automatische Sicherung.
    /// </summary>
    public class SnapshotManager
    {
        private static readonly Lazy<SnapshotManager> _instance = new(() => new SnapshotManager());
        public static SnapshotManager Instance => _instance.Value;

        private readonly string _snapshotsDirectory;
        private readonly List<StateSnapshot> _snapshots = [];
        private readonly Dictionary<string, Func<object>> _registeredProviders = [];
        private readonly Dictionary<string, Action<object>> _registeredRestoreHandlers = [];
        private readonly object _lock = new();
        private readonly SettingsService<ApplicationSettings> _settingsService;
        private System.Timers.Timer? _autoSnapshotTimer;

        public event EventHandler<StateSnapshot>? SnapshotCreated;
        public event EventHandler<StateSnapshot>? SnapshotRestored;
        public event EventHandler<string>? SnapshotDeleted;
        public event EventHandler<SnapshotCategory>? CategorySnapshotCreated;

        /// <summary>
        /// Aktuelle Anwendungseinstellungen.
        /// </summary>
        public ApplicationSettings Settings => _settingsService.Value;

        /// <summary>
        /// Gibt an ob Auto-Snapshots aktiviert sind.
        /// </summary>
        public bool IsAutoSnapshotEnabled => Settings.AutoSnapshotsEnabled;

        private SnapshotManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _snapshotsDirectory = Path.Combine(appData, "Wpf", "Snapshots");
            Directory.CreateDirectory(_snapshotsDirectory);

            _settingsService = new SettingsService<ApplicationSettings>("app-settings.json");
            LoadSnapshots();

            if (Settings.AutoSnapshotsEnabled)
            {
                StartAutoSnapshot();
            }
        }

        #region Provider Registration

        /// <summary>
        /// Registriert einen Daten-Provider für eine bestimmte Kategorie.
        /// </summary>
        /// <typeparam name="T">Typ der Daten</typeparam>
        /// <param name="category">Snapshot-Kategorie</param>
        /// <param name="dataProvider">Funktion die die aktuellen Daten liefert</param>
        /// <param name="restoreHandler">Optionaler Handler zum Wiederherstellen der Daten</param>
        public void RegisterProvider<T>(SnapshotCategory category, Func<T> dataProvider, Action<T>? restoreHandler = null)
        {
            lock (_lock)
            {
                var key = category.ToString();
                _registeredProviders[key] = () => dataProvider()!;

                if (restoreHandler != null)
                {
                    _registeredRestoreHandlers[key] = obj => restoreHandler((T)obj);
                }
            }
        }

        /// <summary>
        /// Entfernt einen registrierten Provider.
        /// </summary>
        public void UnregisterProvider(SnapshotCategory category)
        {
            lock (_lock)
            {
                var key = category.ToString();
                _registeredProviders.Remove(key);
                _registeredRestoreHandlers.Remove(key);
            }
        }

        #endregion

        #region Snapshot Creation

        /// <summary>
        /// Erstellt einen Snapshot für eine bestimmte Kategorie.
        /// </summary>
        public StateSnapshot? CreateCategorySnapshot(SnapshotCategory category, string name)
        {
            if (!IsCategoryEnabled(category)) return null;

            lock (_lock)
            {
                var key = category.ToString();
                if (!_registeredProviders.TryGetValue(key, out var provider))
                {
                    return null;
                }

                var data = provider();
                var snapshot = StateSnapshot.Create(data, name, SnapshotType.Auto, category);

                _snapshots.Add(snapshot);
                SaveSnapshot(snapshot);
                CleanupOldSnapshots();

                TelemetryService.Instance.LogEvent("CategorySnapshot_Created", "Snapshot", new Dictionary<string, object>
                {
                    { "Category", category.ToString() },
                    { "Name", name },
                    { "SizeBytes", snapshot.SizeBytes }
                });

                SnapshotCreated?.Invoke(this, snapshot);
                CategorySnapshotCreated?.Invoke(this, category);
                return snapshot;
            }
        }

        /// <summary>
        /// Erstellt Snapshots für alle registrierten und aktivierten Kategorien.
        /// </summary>
        public List<StateSnapshot> CreateAllCategorySnapshots(string baseName = "Auto")
        {
            var snapshots = new List<StateSnapshot>();

            foreach (var category in Enum.GetValues<SnapshotCategory>())
            {
                if (category == SnapshotCategory.Custom) continue;

                var snapshot = CreateCategorySnapshot(category, $"{baseName} - {category}");
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }

            return snapshots;
        }

        /// <summary>
        /// Erstellt einen generischen Snapshot von beliebigen Daten.
        /// </summary>
        public StateSnapshot CreateSnapshot<T>(T state, string name, SnapshotType type = SnapshotType.Manual, SnapshotCategory category = SnapshotCategory.Custom)
        {
            if (!Settings.AutoSnapshotsEnabled && type == SnapshotType.Auto)
            {
                return StateSnapshot.Create(state, name, type, category); // Nicht speichern
            }

            lock (_lock)
            {
                var snapshot = StateSnapshot.Create(state, name, type, category);

                _snapshots.Add(snapshot);
                SaveSnapshot(snapshot);
                CleanupOldSnapshots();

                TelemetryService.Instance.LogEvent("Snapshot_Created", "Snapshot", new Dictionary<string, object>
                {
                    { "Type", type.ToString() },
                    { "Category", category.ToString() },
                    { "Name", name },
                    { "SizeBytes", snapshot.SizeBytes }
                });

                SnapshotCreated?.Invoke(this, snapshot);
                return snapshot;
            }
        }

        #endregion

        #region Snapshot Retrieval

        /// <summary>
        /// Alle Snapshots abrufen.
        /// </summary>
        public IReadOnlyList<StateSnapshot> GetSnapshots()
        {
            lock (_lock)
            {
                return _snapshots.OrderByDescending(s => s.Timestamp).ToList();
            }
        }

        /// <summary>
        /// Snapshots nach Typ abrufen.
        /// </summary>
        public IReadOnlyList<StateSnapshot> GetSnapshotsByType(SnapshotType type)
        {
            lock (_lock)
            {
                return _snapshots
                    .Where(s => s.Type == type)
                    .OrderByDescending(s => s.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Snapshots nach Kategorie abrufen.
        /// </summary>
        public IReadOnlyList<StateSnapshot> GetSnapshotsByCategory(SnapshotCategory category)
        {
            lock (_lock)
            {
                return _snapshots
                    .Where(s => s.Category == category)
                    .OrderByDescending(s => s.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Snapshot nach ID abrufen.
        /// </summary>
        public StateSnapshot? GetSnapshot(string id)
        {
            lock (_lock)
            {
                return _snapshots.FirstOrDefault(s => s.Id == id);
            }
        }

        /// <summary>
        /// Neuesten Snapshot abrufen.
        /// </summary>
        public StateSnapshot? GetLatestSnapshot()
        {
            lock (_lock)
            {
                return _snapshots.OrderByDescending(s => s.Timestamp).FirstOrDefault();
            }
        }

        /// <summary>
        /// Neuesten Snapshot einer Kategorie abrufen.
        /// </summary>
        public StateSnapshot? GetLatestSnapshot(SnapshotCategory category)
        {
            lock (_lock)
            {
                return _snapshots
                    .Where(s => s.Category == category)
                    .OrderByDescending(s => s.Timestamp)
                    .FirstOrDefault();
            }
        }

        #endregion

        #region Snapshot Restoration

        /// <summary>
        /// Stellt Daten aus einem Snapshot wieder her.
        /// </summary>
        public T? RestoreSnapshot<T>(string snapshotId)
        {
            lock (_lock)
            {
                var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
                if (snapshot == null)
                    throw new InvalidOperationException($"Snapshot '{snapshotId}' nicht gefunden");

                var state = snapshot.Restore<T>();

                TelemetryService.Instance.LogEvent("Snapshot_Restored", "Snapshot", new Dictionary<string, object>
                {
                    { "SnapshotId", snapshotId },
                    { "Name", snapshot.Name }
                });

                SnapshotRestored?.Invoke(this, snapshot);
                return state;
            }
        }

        /// <summary>
        /// Stellt einen Kategorie-Snapshot wieder her und ruft den registrierten Handler auf.
        /// </summary>
        public bool RestoreCategorySnapshot(string snapshotId)
        {
            lock (_lock)
            {
                var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
                if (snapshot == null) return false;

                var key = snapshot.Category.ToString();
                if (!_registeredRestoreHandlers.TryGetValue(key, out var handler))
                {
                    return false;
                }

                var data = snapshot.Restore<object>();
                if (data == null) return false;

                handler(data);

                TelemetryService.Instance.LogEvent("CategorySnapshot_Restored", "Snapshot", new Dictionary<string, object>
                {
                    { "SnapshotId", snapshotId },
                    { "Category", snapshot.Category.ToString() }
                });

                SnapshotRestored?.Invoke(this, snapshot);
                return true;
            }
        }

        #endregion

        #region Snapshot Management

        /// <summary>
        /// Löscht einen Snapshot.
        /// </summary>
        public bool DeleteSnapshot(string snapshotId)
        {
            lock (_lock)
            {
                var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
                if (snapshot == null)
                    return false;

                _snapshots.Remove(snapshot);
                DeleteSnapshotFile(snapshot.Id);

                TelemetryService.Instance.LogEvent("Snapshot_Deleted", "Snapshot", new Dictionary<string, object>
                {
                    { "SnapshotId", snapshotId }
                });

                SnapshotDeleted?.Invoke(this, snapshotId);
                return true;
            }
        }

        /// <summary>
        /// Löscht alle Snapshots.
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                _snapshots.Clear();

                foreach (var file in Directory.GetFiles(_snapshotsDirectory, "*.json"))
                {
                    try { File.Delete(file); } catch { }
                }

                TelemetryService.Instance.LogEvent("Snapshots_Cleared");
            }
        }

        /// <summary>
        /// Löscht alle Snapshots einer Kategorie.
        /// </summary>
        public void ClearCategory(SnapshotCategory category)
        {
            lock (_lock)
            {
                var toRemove = _snapshots.Where(s => s.Category == category).ToList();
                foreach (var snapshot in toRemove)
                {
                    _snapshots.Remove(snapshot);
                    DeleteSnapshotFile(snapshot.Id);
                }
            }
        }

        /// <summary>
        /// Gesamtgröße aller Snapshots abrufen.
        /// </summary>
        public long GetTotalSize()
        {
            lock (_lock)
            {
                return _snapshots.Sum(s => s.SizeBytes);
            }
        }

        #endregion

        #region Settings Management

        /// <summary>
        /// Aktiviert oder deaktiviert Auto-Snapshots.
        /// </summary>
        public void SetAutoSnapshotEnabled(bool enabled)
        {
            Settings.AutoSnapshotsEnabled = enabled;
            _ = _settingsService.SaveAsync();

            if (enabled)
            {
                StartAutoSnapshot();
            }
            else
            {
                StopAutoSnapshot();
            }
        }

        /// <summary>
        /// Setzt das Auto-Snapshot-Intervall in Minuten.
        /// </summary>
        public void SetAutoSnapshotInterval(int minutes)
        {
            Settings.AutoSnapshotIntervalMinutes = Math.Max(1, minutes);
            _ = _settingsService.SaveAsync();

            if (_autoSnapshotTimer != null)
            {
                _autoSnapshotTimer.Interval = TimeSpan.FromMinutes(Settings.AutoSnapshotIntervalMinutes).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Speichert die Einstellungen asynchron.
        /// </summary>
        public Task SaveSettingsAsync() => _settingsService.SaveAsync();

        #endregion

        #region Private Methods

        private bool IsCategoryEnabled(SnapshotCategory category)
        {
            if (!Settings.AutoSnapshotsEnabled) return false;

            return category switch
            {
                SnapshotCategory.WindowLayout => Settings.SnapshotCategories.WindowLayout,
                SnapshotCategory.UserSettings => Settings.SnapshotCategories.UserSettings,
                SnapshotCategory.AppConfiguration => Settings.SnapshotCategories.AppConfiguration,
                SnapshotCategory.SessionData => Settings.SnapshotCategories.SessionData,
                SnapshotCategory.Custom => Settings.SnapshotCategories.CustomData,
                _ => true
            };
        }

        private void StartAutoSnapshot()
        {
            StopAutoSnapshot();

            _autoSnapshotTimer = new System.Timers.Timer(TimeSpan.FromMinutes(Settings.AutoSnapshotIntervalMinutes).TotalMilliseconds);
            _autoSnapshotTimer.Elapsed += OnAutoSnapshotTimerElapsed;
            _autoSnapshotTimer.AutoReset = true;
            _autoSnapshotTimer.Start();
        }

        private void StopAutoSnapshot()
        {
            if (_autoSnapshotTimer != null)
            {
                _autoSnapshotTimer.Stop();
                _autoSnapshotTimer.Elapsed -= OnAutoSnapshotTimerElapsed;
                _autoSnapshotTimer.Dispose();
                _autoSnapshotTimer = null;
            }
        }

        private void OnAutoSnapshotTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            CreateAllCategorySnapshots("Auto-Timer");
        }

        private void SaveSnapshot(StateSnapshot snapshot)
        {
            try
            {
                var filePath = Path.Combine(_snapshotsDirectory, $"{snapshot.Id}.json");
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                TelemetryService.Instance.LogError("Snapshot_SaveError", ex, null);
            }
        }

        private void LoadSnapshots()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_snapshotsDirectory, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var snapshot = JsonSerializer.Deserialize<StateSnapshot>(json);
                        if (snapshot != null)
                        {
                            _snapshots.Add(snapshot);
                        }
                    }
                    catch
                    {
                        // Skip corrupted files
                    }
                }
            }
            catch (Exception ex)
            {
                TelemetryService.Instance.LogError("Snapshots_LoadError", ex, null);
            }
        }

        private void CleanupOldSnapshots()
        {
            if (_snapshots.Count <= Settings.MaxSnapshotCount)
                return;

            var toRemove = _snapshots
                .Where(s => s.Type == SnapshotType.Auto)
                .OrderBy(s => s.Timestamp)
                .Take(_snapshots.Count - Settings.MaxSnapshotCount)
                .ToList();

            foreach (var snapshot in toRemove)
            {
                _snapshots.Remove(snapshot);
                DeleteSnapshotFile(snapshot.Id);
            }
        }

        private void DeleteSnapshotFile(string snapshotId)
        {
            try
            {
                var filePath = Path.Combine(_snapshotsDirectory, $"{snapshotId}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { }
        }

        #endregion
    }
}
