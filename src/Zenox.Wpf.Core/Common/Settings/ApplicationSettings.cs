namespace Zenox.Wpf.Core.Common.Settings
{
    /// <summary>
    /// Zentrale Anwendungseinstellungen mit Snapshot-Konfiguration.
    /// </summary>
    public class ApplicationSettings
    {
        /// <summary>
        /// Aktiviert/Deaktiviert das automatische Erstellen von Snapshots.
        /// </summary>
        public bool AutoSnapshotsEnabled { get; set; } = true;

        /// <summary>
        /// Intervall in Minuten für automatische Snapshots.
        /// </summary>
        public int AutoSnapshotIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// Maximale Anzahl an Snapshots die aufbewahrt werden.
        /// </summary>
        public int MaxSnapshotCount { get; set; } = 50;

        /// <summary>
        /// Snapshot bei Anwendungsstart erstellen.
        /// </summary>
        public bool SnapshotOnStartup { get; set; } = true;

        /// <summary>
        /// Snapshot bei Anwendungsende erstellen.
        /// </summary>
        public bool SnapshotOnShutdown { get; set; } = true;

        /// <summary>
        /// Kategorien die für Snapshots aktiviert sind.
        /// </summary>
        public SnapshotCategorySettings SnapshotCategories { get; set; } = new();
    }

    /// <summary>
    /// Einstellungen für Snapshot-Kategorien.
    /// </summary>
    public class SnapshotCategorySettings
    {
        /// <summary>
        /// Fenster-Layout und Position speichern.
        /// </summary>
        public bool WindowLayout { get; set; } = true;

        /// <summary>
        /// Benutzereinstellungen speichern.
        /// </summary>
        public bool UserSettings { get; set; } = true;

        /// <summary>
        /// Anwendungskonfiguration speichern.
        /// </summary>
        public bool AppConfiguration { get; set; } = true;

        /// <summary>
        /// Session-Daten speichern.
        /// </summary>
        public bool SessionData { get; set; } = true;

        /// <summary>
        /// Benutzerdefinierte Daten speichern.
        /// </summary>
        public bool CustomData { get; set; } = true;
    }
}
