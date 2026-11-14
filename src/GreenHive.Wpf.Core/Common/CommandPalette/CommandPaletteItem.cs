using System.Windows.Input;

namespace GreenHive.NET.Core.Common.CommandPalette
{
	/// <summary>
	/// Repräsentiert einen einzelnen Befehl in der Command Palette
	/// </summary>
	public class CommandPaletteItem
	{
		/// <summary>
		/// Der Anzeigename des Befehls
		/// </summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>
		/// Beschreibung des Befehls
		/// </summary>
		public string? Description { get; set; }

		/// <summary>
		/// Kategorie für die Gruppierung (z.B. "Navigation", "Einstellungen", "Hilfe")
		/// </summary>
		public string Category { get; set; } = "Allgemein";

		/// <summary>
		/// Optionaler Icon-Pfad
		/// </summary>
		public string? IconPath { get; set; }

		/// <summary>
		/// Tastenkürzel für direkten Zugriff
		/// </summary>
		public string? Shortcut { get; set; }

		/// <summary>
		/// Der auszuführende Befehl
		/// </summary>
		public ICommand? Command { get; set; }

		/// <summary>
		/// Parameter für den Befehl
		/// </summary>
		public object? CommandParameter { get; set; }

		/// <summary>
		/// Suchbare Schlüsselwörter für bessere Auffindbarkeit
		/// </summary>
		public string[] Keywords { get; set; } = Array.Empty<string>();

		/// <summary>
		/// Berechnet eine Übereinstimmungsgewichtung für die Fuzzy-Suche
		/// </summary>
		public int CalculateMatchScore(string searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
				return 0;

			searchText = searchText.ToLowerInvariant();
			var name = Name.ToLowerInvariant();
			var description = Description?.ToLowerInvariant() ?? string.Empty;
			var category = Category.ToLowerInvariant();

			int score = 0;

			// Exakte Übereinstimmung = höchste Priorität
			if (name == searchText)
				return 1000;

			// Startet mit Suchtext = hohe Priorität
			if (name.StartsWith(searchText))
				score += 100;

			// Enthält Suchtext im Namen
			if (name.Contains(searchText))
				score += 50;

			// Enthält in Beschreibung
			if (description.Contains(searchText))
				score += 20;

			// Enthält in Kategorie
			if (category.Contains(searchText))
				score += 10;

			// Keywords prüfen
			foreach (var keyword in Keywords)
			{
				if (keyword.ToLowerInvariant().Contains(searchText))
					score += 30;
			}

			return score;
		}
	}
}
