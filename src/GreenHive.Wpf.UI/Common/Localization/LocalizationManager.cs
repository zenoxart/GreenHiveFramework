using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace GreenHive.Wpf.UI.Common.Localization
{
    /// <summary>
    /// Verwaltet die Lokalisierung der Anwendung
    /// </summary>
    public class LocalizationManager
    {
        private static LocalizationManager? _instance;
        private static readonly object _lock = new();
        private string _currentLanguage = "de-DE";
        private readonly string _settingsPath;
        private readonly Dictionary<string, ResourceDictionary> _languageResources = new();

        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LocalizationManager();
                    }
                }
                return _instance;
            }
        }

        public event EventHandler<string>? LanguageChanged;

        private LocalizationManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appData, "GreenHive", "Settings");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "LanguageSettings.json");

            LoadLanguageResources();
            LoadSettings();
        }

        /// <summary>
        /// Lädt alle verfügbaren Sprachressourcen
        /// </summary>
        private void LoadLanguageResources()
        {
            try
            {
                // Lade deutsche Ressourcen
                try
                {
                    var deDict = new ResourceDictionary
                    {
                        Source = new Uri("/GreenHive.Wpf.UI;component/Resources/Localization/de-DE.xaml", UriKind.Relative)
                    };
                    _languageResources["de-DE"] = deDict;
                    System.Diagnostics.Debug.WriteLine("Deutsche Ressourcen erfolgreich geladen");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der deutschen Ressourcen: {ex.Message}");
                }

                // Lade englische Ressourcen
                try
                {
                    var enDict = new ResourceDictionary
                    {
                        Source = new Uri("/GreenHive.Wpf.UI;component/Resources/Localization/en-US.xaml", UriKind.Relative)
                    };
                    _languageResources["en-US"] = enDict;
                    System.Diagnostics.Debug.WriteLine("Englische Ressourcen erfolgreich geladen");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der englischen Ressourcen: {ex.Message}");
                }

                // Fallback: Wenn keine Ressourcen geladen wurden, verwende eine leere ResourceDictionary
                if (_languageResources.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("WARNUNG: Keine Sprachressourcen geladen. Verwende Fallback.");
                    _languageResources["de-DE"] = new ResourceDictionary();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Kritischer Fehler beim Laden der Sprachressourcen: {ex.Message}");
                // Erstelle minimale Fallback-Ressourcen
                _languageResources["de-DE"] = new ResourceDictionary();
            }
        }

        /// <summary>
        /// Lädt die gespeicherten Einstellungen
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<LanguageSettings>(json);
                    if (settings != null && !string.IsNullOrEmpty(settings.CurrentLanguage))
                    {
                        _currentLanguage = settings.CurrentLanguage;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Spracheinstellungen: {ex.Message}");
            }
        }

        /// <summary>
        /// Speichert die aktuellen Einstellungen
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                var settings = new LanguageSettings { CurrentLanguage = _currentLanguage };
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Speichern der Spracheinstellungen: {ex.Message}");
            }
        }

        /// <summary>
        /// Wechselt die aktuelle Sprache
        /// </summary>
        public void ChangeLanguage(string languageCode)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== ChangeLanguage aufgerufen: {languageCode} ===");
                
                if (!_languageResources.ContainsKey(languageCode))
                {
                    System.Diagnostics.Debug.WriteLine($"? Sprache {languageCode} nicht gefunden. Verwende Fallback.");
                    
                    // Verwende Deutsch als Fallback
                    if (_languageResources.ContainsKey("de-DE"))
                    {
                        languageCode = "de-DE";
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("? KRITISCH: Keine Sprachressourcen verfügbar!");
                        return;
                    }
                }

                _currentLanguage = languageCode;
                System.Diagnostics.Debug.WriteLine($"? Neue Sprache gesetzt: {_currentLanguage}");

                // Wende die Sprachressourcen an
                ApplyLanguageResources(languageCode);

                // Speichere die Einstellung
                SaveSettings();
                System.Diagnostics.Debug.WriteLine($"? Spracheinstellung gespeichert");

                // Benachrichtige Listeners
                LanguageChanged?.Invoke(this, languageCode);
                System.Diagnostics.Debug.WriteLine($"? LanguageChanged Event ausgelöst");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Kritischer Fehler beim Sprachwechsel: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }


        /// <summary>
        /// Gibt die aktuelle Sprache zurück
        /// </summary>
        public string GetCurrentLanguage()
        {
            return _currentLanguage;
        }

        /// <summary>
        /// Gibt alle verfügbaren Sprachen zurück
        /// </summary>
        public List<LanguageInfo> GetAvailableLanguages()
        {
            return new List<LanguageInfo>
            {
                new LanguageInfo { Code = "de-DE", Name = "Deutsch", NativeName = "Deutsch" },
                new LanguageInfo { Code = "en-US", Name = "English", NativeName = "English" }
            };
        }

        /// <summary>
        /// Initialisiert die Lokalisierung beim Start
        /// </summary>
        public void Initialize()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Lokalisierung wird initialisiert ===");
                System.Diagnostics.Debug.WriteLine($"Gespeicherte Sprache: {_currentLanguage}");
                System.Diagnostics.Debug.WriteLine($"Verfügbare Ressourcen: {_languageResources.Count}");
                
                // Validiere, dass die gespeicherte Sprache verfügbar ist
                if (!_languageResources.ContainsKey(_currentLanguage))
                {
                    System.Diagnostics.Debug.WriteLine($"? Gespeicherte Sprache {_currentLanguage} nicht verfügbar. Verwende de-DE als Fallback.");
                    _currentLanguage = "de-DE";
                }
                
                // WICHTIG: Lade die Ressourcen SOFORT in Application.Resources
                ApplyLanguageResources(_currentLanguage);
                
                System.Diagnostics.Debug.WriteLine($"? Lokalisierung erfolgreich initialisiert mit Sprache: {_currentLanguage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Fehler beim Initialisieren der Lokalisierung: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                
                // Letzter Versuch: Lade Deutsch direkt
                try
                {
                    _currentLanguage = "de-DE";
                    ApplyLanguageResources("de-DE");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("? KRITISCH: Lokalisierung konnte nicht initialisiert werden!");
                }
            }
        }

        /// <summary>
        /// Wendet die Sprachressourcen auf Application.Resources an
        /// </summary>
        private void ApplyLanguageResources(string languageCode)
        {
            try
            {
                if (!_languageResources.ContainsKey(languageCode))
                {
                    System.Diagnostics.Debug.WriteLine($"? Sprache {languageCode} nicht in _languageResources gefunden");
                    return;
                }

                var app = Application.Current;
                if (app == null)
                {
                    System.Diagnostics.Debug.WriteLine("? Application.Current ist NULL!");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Entferne alte Sprachressourcen...");
                
                // Entferne alte Sprachressourcen
                var oldResources = app.Resources.MergedDictionaries
                    .Where(d => d.Source != null && d.Source.OriginalString.Contains("/Localization/"))
                    .ToList();

                foreach (var resource in oldResources)
                {
                    System.Diagnostics.Debug.WriteLine($"  Entferne: {resource.Source.OriginalString}");
                    app.Resources.MergedDictionaries.Remove(resource);
                }

                // Füge neue Sprachressourcen hinzu
                var newResource = _languageResources[languageCode];
                
                // Prüfe auf null-Werte in den Ressourcen
                var nullKeys = new List<object>();
                foreach (var key in newResource.Keys)
                {
                    if (newResource[key] == null)
                    {
                        nullKeys.Add(key);
                        System.Diagnostics.Debug.WriteLine($"  ? NULL-Wert gefunden für Key: {key}");
                    }
                }
                
                // Entferne null-Werte aus dem Dictionary
                foreach (var key in nullKeys)
                {
                    newResource.Remove(key);
                }
                
                app.Resources.MergedDictionaries.Add(newResource);
                System.Diagnostics.Debug.WriteLine($"? Sprachressource hinzugefügt: {languageCode} ({newResource.Count} keys, {nullKeys.Count} null-Werte entfernt)");
                
                // Liste die ersten 5 Keys auf
                int count = 0;
                foreach (var key in newResource.Keys)
                {
                    if (count++ < 5)
                    {
                        var value = newResource[key];
                        var valueStr = value?.ToString() ?? "<null>";
                        System.Diagnostics.Debug.WriteLine($"  Key: {key} = {valueStr}");
                    }
                }

                // Aktualisiere CultureInfo
                try
                {
                    var culture = new CultureInfo(languageCode);
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                    System.Diagnostics.Debug.WriteLine($"? CultureInfo gesetzt: {culture.DisplayName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? Fehler beim Setzen der Culture: {ex.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"? Sprachressourcen erfolgreich angewendet: {languageCode}");
                System.Diagnostics.Debug.WriteLine($"? Gesamt MergedDictionaries: {app.Resources.MergedDictionaries.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Fehler beim Anwenden der Sprachressourcen: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }
    }



    /// <summary>
    /// Einstellungen für die Sprache
    /// </summary>
    public class LanguageSettings
    {
        public string CurrentLanguage { get; set; } = "de-DE";
    }

    /// <summary>
    /// Informationen über eine Sprache
    /// </summary>
    public class LanguageInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
    }
}
