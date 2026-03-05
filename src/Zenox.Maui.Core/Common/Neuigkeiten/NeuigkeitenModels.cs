using System.Collections.ObjectModel;

namespace Zenox.NET.Core.Common.Neuigkeiten
{
    /// <summary>
    /// Repräsentiert einen einzelnen Eintrag in einer Neuigkeiten-Kategorie
    /// </summary>
    public class NeuigkeitEintrag
    {
        /// <summary>
        /// Text des Eintrags
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Optionale Beschreibung
        /// </summary>
        public string? Beschreibung { get; set; }

        /// <summary>
        /// Optionaler Link für mehr Informationen
        /// </summary>
        public string? Link { get; set; }

        /// <summary>
        /// Icon/Emoji für den Eintrag
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Priorität für Sortierung (höher = wichtiger)
        /// </summary>
        public int Prioritaet { get; set; } = 0;
    }

    /// <summary>
    /// Repräsentiert eine Kategorie von Neuigkeiten (z.B. "What's new", "Breaking Changes", "Bug Fixes")
    /// </summary>
    public class NeuigkeitenKategorie
    {
        /// <summary>
        /// Titel der Kategorie
        /// </summary>
        public string Titel { get; set; } = string.Empty;

        /// <summary>
        /// Icon/Emoji für die Kategorie
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Farbe für die Kategorie (Hex-String)
        /// </summary>
        public string? Farbe { get; set; }

        /// <summary>
        /// Liste der Einträge in dieser Kategorie
        /// </summary>
        public ObservableCollection<NeuigkeitEintrag> Eintraege { get; set; } = new();
    }

    /// <summary>
    /// Repräsentiert eine komplette Version mit allen Neuigkeiten
    /// </summary>
    public class VersionNeuigkeiten
    {
        /// <summary>
        /// Versionsnummer (z.B. "1.0", "2.1.0")
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Veröffentlichungsdatum
        /// </summary>
        public DateTime? Datum { get; set; }

        /// <summary>
        /// Release-Typ (z.B. "Major", "Minor", "Patch", "Beta")
        /// </summary>
        public string? ReleaseTyp { get; set; }

        /// <summary>
        /// Kategorien mit Neuigkeiten für diese Version
        /// </summary>
        public ObservableCollection<NeuigkeitenKategorie> Kategorien { get; set; } = new();

        /// <summary>
        /// Formatiertes Datum für Anzeige
        /// </summary>
        public string DatumFormatiert => Datum?.ToString("dd.MM.yyyy") ?? "Unbekannt";
    }

    /// <summary>
    /// Root-Element für die Neuigkeiten-Konfiguration
    /// </summary>
    public class NeuigkeitenKonfiguration
    {
        /// <summary>
        /// Titel der Neuigkeiten-Seite
        /// </summary>
        public string Titel { get; set; } = "Neuigkeiten";

        /// <summary>
        /// Untertitel
        /// </summary>
        public string? Untertitel { get; set; }

        /// <summary>
        /// Liste aller Versionen mit Neuigkeiten
        /// </summary>
        public ObservableCollection<VersionNeuigkeiten> Versionen { get; set; } = new();

        /// <summary>
        /// Anzeige-Einstellungen für die Neuigkeiten
        /// </summary>
        public AnzeigeEinstellungen AnzeigeEinstellungen { get; set; } = new();
    }

    /// <summary>
    /// Anzeige-Einstellungen für die Neuigkeiten-Anzeige
    /// </summary>
    public class AnzeigeEinstellungen
    {
        /// <summary>
        /// Sortierreihenfolge (z.B. "Datum", "Priorität")
        /// </summary>
        public string SortOrder { get; set; } = "Datum";

        /// <summary>
        /// Kategorien anzeigen (true/false)
        /// </summary>
        public bool ShowCategories { get; set; } = true;

        /// <summary>
        /// Theme (z.B. "Light", "Dark")
        /// </summary>
        public string Theme { get; set; } = "Light";
    }
}
