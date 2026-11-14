using System.Collections.ObjectModel;
using System.Windows.Markup;

namespace GreenHive.NET.Core.Common.Neuigkeiten
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
	}

	/// <summary>
	/// Repräsentiert eine Kategorie von Neuigkeiten (z.B. "What's new", "Breaking Changes")
	/// </summary>
	[ContentProperty(nameof(Eintraege))]
	public class NeuigkeitenKategorie
	{
		/// <summary>
		/// Titel der Kategorie
		/// </summary>
		public string Titel { get; set; } = string.Empty;

		/// <summary>
		/// Liste der Einträge in dieser Kategorie
		/// </summary>
		public ObservableCollection<NeuigkeitEintrag> Eintraege { get; set; } = new();
	}

	/// <summary>
	/// Repräsentiert eine komplette Version mit allen Neuigkeiten
	/// </summary>
	[ContentProperty(nameof(Kategorien))]
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
		/// Kategorien mit Neuigkeiten für diese Version
		/// </summary>
		public ObservableCollection<NeuigkeitenKategorie> Kategorien { get; set; } = new();
	}

	/// <summary>
	/// Root-Element für die Neuigkeiten-Konfiguration
	/// </summary>
	[ContentProperty(nameof(Versionen))]
	public class NeuigkeitenKonfiguration
	{
		/// <summary>
		/// Titel der Neuigkeiten-Seite
		/// </summary>
		public string Titel { get; set; } = "Neuigkeiten";

		/// <summary>
		/// Liste aller Versionen mit Neuigkeiten
		/// </summary>
		public ObservableCollection<VersionNeuigkeiten> Versionen { get; set; } = new();
	}
}
