using System.Text.Json.Serialization;

namespace Zenox.Wpf.UI.Common.Settings
{
    /// <summary>
    /// Speichert die Einstellungen für ein Fenster
    /// </summary>
    public class WindowSettings
    {
        /// <summary>
        /// Eindeutiger Bezeichner für das Fenster
        /// </summary>
        [JsonPropertyName("windowId")]
        public string WindowId { get; set; } = string.Empty;

        /// <summary>
        /// X-Position des Fensters
        /// </summary>
        [JsonPropertyName("left")]
        public double Left { get; set; }

        /// <summary>
        /// Y-Position des Fensters
        /// </summary>
        [JsonPropertyName("top")]
        public double Top { get; set; }

        /// <summary>
        /// Breite des Fensters
        /// </summary>
        [JsonPropertyName("width")]
        public double Width { get; set; } = 1280;

        /// <summary>
        /// Höhe des Fensters
        /// </summary>
        [JsonPropertyName("height")]
        public double Height { get; set; } = 720;

        /// <summary>
        /// Fensterzustand (Normal, Minimized, Maximized)
        /// </summary>
        [JsonPropertyName("windowState")]
        public string WindowState { get; set; } = "Normal";

        /// <summary>
        /// Zeitstempel der letzten Aktualisierung
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Monitor-Index, auf dem das Fenster angezeigt wurde
        /// </summary>
        [JsonPropertyName("monitorIndex")]
        public int MonitorIndex { get; set; } = 0;

        /// <summary>
        /// Gibt an, ob die Einstellungen gültig sind
        /// </summary>
        [JsonPropertyName("isValid")]
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    /// Verwaltet alle gespeicherten Fenstereinstellungen
    /// </summary>
    public class WindowSettingsCollection
    {
        /// <summary>
        /// Liste aller gespeicherten Fenstereinstellungen
        /// </summary>
        [JsonPropertyName("windows")]
        public List<WindowSettings> Windows { get; set; } = new();

        /// <summary>
        /// Automatisch Fensterposition beim Start wiederherstellen
        /// </summary>
        [JsonPropertyName("restoreOnStartup")]
        public bool RestoreOnStartup { get; set; } = true;

        /// <summary>
        /// Maximale Anzahl an gespeicherten Fenstern
        /// </summary>
        [JsonPropertyName("maxWindowCount")]
        public int MaxWindowCount { get; set; } = 10;

        /// <summary>
        /// Automatisch speichern bei Änderungen
        /// </summary>
        [JsonPropertyName("autoSave")]
        public bool AutoSave { get; set; } = true;

        /// <summary>
        /// Mehrere Instanzen unterstützen
        /// </summary>
        [JsonPropertyName("supportMultipleInstances")]
        public bool SupportMultipleInstances { get; set; } = true;
    }
}
