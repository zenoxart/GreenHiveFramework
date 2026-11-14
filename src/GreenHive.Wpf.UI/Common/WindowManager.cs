using System.IO;
using System.Text.Json;
using System.Windows;
using GreenHive.Wpf.UI.Common.Settings;

namespace GreenHive.Wpf.UI.Common
{
    /// <summary>
    /// Verwaltet Fenstereinstellungen und unterstützt mehrere Instanzen
    /// </summary>
    public class WindowManager
    {
        private static WindowManager? _instance;
        private static readonly object _lock = new();
        private readonly string _settingsPath;
        private WindowSettingsCollection _settings;
        private readonly Dictionary<string, System.Windows.Window> _trackedWindows = new();

        public static WindowManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new WindowManager();
                    }
                }
                return _instance;
            }
        }

        private WindowManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appData, "GreenHive", "Settings");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "WindowSettings.json");
            
            _settings = LoadSettings();
        }

        /// <summary>
        /// Lädt die gespeicherten Einstellungen
        /// </summary>
        private WindowSettingsCollection LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<WindowSettingsCollection>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Fenstereinstellungen: {ex.Message}");
            }

            return new WindowSettingsCollection();
        }

        /// <summary>
        /// Speichert die aktuellen Einstellungen
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Speichern der Fenstereinstellungen: {ex.Message}");
            }
        }

        /// <summary>
        /// Registriert ein Fenster für automatisches Tracking
        /// </summary>
        public void TrackWindow(System.Windows.Window window, string? windowId = null)
        {
            if (window == null) return;

            var id = windowId ?? window.GetType().Name;
            
            if (_trackedWindows.ContainsKey(id))
            {
                // Wenn mehrere Instanzen unterstützt werden, füge einen Suffix hinzu
                if (_settings.SupportMultipleInstances)
                {
                    var index = 1;
                    while (_trackedWindows.ContainsKey($"{id}_{index}"))
                    {
                        index++;
                    }
                    id = $"{id}_{index}";
                }
                else
                {
                    return; // Fenster bereits getrackt
                }
            }

            _trackedWindows[id] = window;

            // Stelle gespeicherte Position wieder her
            if (_settings.RestoreOnStartup)
            {
                RestoreWindowPosition(window, id);
            }

            // Event-Handler registrieren
            window.LocationChanged += (s, e) => OnWindowChanged(id);
            window.SizeChanged += (s, e) => OnWindowChanged(id);
            window.StateChanged += (s, e) => OnWindowChanged(id);
            window.Closing += (s, e) => OnWindowClosing(id);
        }

        /// <summary>
        /// Stellt die gespeicherte Fensterposition wieder her
        /// </summary>
        private void RestoreWindowPosition(System.Windows.Window window, string windowId)
        {
            var settings = GetWindowSettings(windowId);
            if (settings == null || !settings.IsValid) return;

            try
            {
                // Prüfe, ob die Position auf einem verfügbaren Monitor liegt
                if (IsPositionValid(settings.Left, settings.Top))
                {
                    window.Left = settings.Left;
                    window.Top = settings.Top;
                }

                window.Width = Math.Max(settings.Width, window.MinWidth);
                window.Height = Math.Max(settings.Height, window.MinHeight);

                // Setze WindowState
                if (Enum.TryParse<WindowState>(settings.WindowState, out var state))
                {
                    window.WindowState = state;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Wiederherstellen der Fensterposition: {ex.Message}");
            }
        }

        /// <summary>
        /// Prüft, ob eine Position auf einem verfügbaren Monitor liegt
        /// </summary>
        private bool IsPositionValid(double left, double top)
        {
            var virtualScreenWidth = SystemParameters.VirtualScreenWidth;
            var virtualScreenHeight = SystemParameters.VirtualScreenHeight;

            return left >= 0 && left < virtualScreenWidth &&
                   top >= 0 && top < virtualScreenHeight;
        }

        /// <summary>
        /// Wird aufgerufen, wenn sich Fenstereinstellungen ändern
        /// </summary>
        private void OnWindowChanged(string windowId)
        {
            if (!_settings.AutoSave) return;

            if (_trackedWindows.TryGetValue(windowId, out var window))
            {
                SaveWindowPosition(window, windowId);
            }
        }

        /// <summary>
        /// Wird aufgerufen, wenn ein Fenster geschlossen wird
        /// </summary>
        private void OnWindowClosing(string windowId)
        {
            if (_trackedWindows.TryGetValue(windowId, out var window))
            {
                SaveWindowPosition(window, windowId);
                _trackedWindows.Remove(windowId);
            }
        }

        /// <summary>
        /// Speichert die aktuelle Fensterposition
        /// </summary>
        public void SaveWindowPosition(System.Windows.Window window, string windowId)
        {
            if (window == null) return;

            try
            {
                var settings = GetWindowSettings(windowId) ?? new WindowSettings { WindowId = windowId };

                // Speichere nur, wenn das Fenster nicht minimiert ist
                if (window.WindowState != WindowState.Minimized)
                {
                    if (window.WindowState == WindowState.Normal)
                    {
                        settings.Left = window.Left;
                        settings.Top = window.Top;
                        settings.Width = window.Width;
                        settings.Height = window.Height;
                    }

                    settings.WindowState = window.WindowState.ToString();
                    settings.LastUpdated = DateTime.Now;
                    settings.IsValid = true;

                    // Aktualisiere oder füge hinzu
                    var existingIndex = _settings.Windows.FindIndex(w => w.WindowId == windowId);
                    if (existingIndex >= 0)
                    {
                        _settings.Windows[existingIndex] = settings;
                    }
                    else
                    {
                        _settings.Windows.Add(settings);
                        CleanupOldWindows();
                    }

                    if (_settings.AutoSave)
                    {
                        SaveSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Speichern der Fensterposition: {ex.Message}");
            }
        }

        /// <summary>
        /// Entfernt alte Fenstereinstellungen, wenn die maximale Anzahl überschritten wird
        /// </summary>
        private void CleanupOldWindows()
        {
            if (_settings.Windows.Count > _settings.MaxWindowCount)
            {
                var toRemove = _settings.Windows
                    .OrderBy(w => w.LastUpdated)
                    .Take(_settings.Windows.Count - _settings.MaxWindowCount)
                    .ToList();

                foreach (var window in toRemove)
                {
                    _settings.Windows.Remove(window);
                }
            }
        }

        /// <summary>
        /// Gibt die Einstellungen für ein bestimmtes Fenster zurück
        /// </summary>
        public WindowSettings? GetWindowSettings(string windowId)
        {
            return _settings.Windows.FirstOrDefault(w => w.WindowId == windowId);
        }

        /// <summary>
        /// Gibt alle gespeicherten Fenstereinstellungen zurück
        /// </summary>
        public List<WindowSettings> GetAllWindowSettings()
        {
            return _settings.Windows.ToList();
        }

        /// <summary>
        /// Löscht die Einstellungen für ein bestimmtes Fenster
        /// </summary>
        public void DeleteWindowSettings(string windowId)
        {
            var settings = GetWindowSettings(windowId);
            if (settings != null)
            {
                _settings.Windows.Remove(settings);
                SaveSettings();
            }
        }

        /// <summary>
        /// Löscht alle Fenstereinstellungen
        /// </summary>
        public void ClearAllSettings()
        {
            _settings.Windows.Clear();
            SaveSettings();
        }

        /// <summary>
        /// Gibt die globalen Einstellungen zurück
        /// </summary>
        public WindowSettingsCollection GetGlobalSettings()
        {
            return _settings;
        }

        /// <summary>
        /// Aktualisiert die globalen Einstellungen
        /// </summary>
        public void UpdateGlobalSettings(bool restoreOnStartup, int maxWindowCount, bool autoSave, bool supportMultipleInstances)
        {
            _settings.RestoreOnStartup = restoreOnStartup;
            _settings.MaxWindowCount = maxWindowCount;
            _settings.AutoSave = autoSave;
            _settings.SupportMultipleInstances = supportMultipleInstances;
            SaveSettings();
        }

        /// <summary>
        /// Gibt die Anzahl der gespeicherten Fenster zurück
        /// </summary>
        public int GetWindowCount()
        {
            return _settings.Windows.Count;
        }

        /// <summary>
        /// Gibt die Anzahl der aktuell getrackten Fenster zurück
        /// </summary>
        public int GetTrackedWindowCount()
        {
            return _trackedWindows.Count;
        }
    }
}
