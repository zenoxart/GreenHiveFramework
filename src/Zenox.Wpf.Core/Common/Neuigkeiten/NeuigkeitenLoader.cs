using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;

namespace Zenox.NET.Core.Common.Neuigkeiten
{
	/// <summary>
	/// Laedt Neuigkeiten-Konfiguration aus XAML-Dateien
	/// </summary>
	public static class NeuigkeitenLoader
	{
		/// <summary>
		/// Laedt die Neuigkeiten-Konfiguration aus einer XAML-Datei
		/// </summary>
		/// <param name="xamlDateiPfad">Pfad zur XAML-Konfigurationsdatei</param>
		/// <returns>Die geladene Konfiguration oder eine leere Konfiguration bei Fehler</returns>
		public static NeuigkeitenKonfiguration LadeAusDatei(string xamlDateiPfad)
		{
			try
			{
				if (!File.Exists(xamlDateiPfad))
				{
					System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] Konfigurationsdatei nicht gefunden: {xamlDateiPfad}");
					return ErstelleStandardKonfiguration();
				}

				System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] Lade Konfiguration aus: {xamlDateiPfad}");

				// Datei explizit mit UTF-8 Encoding lesen
				var xamlContent = File.ReadAllText(xamlDateiPfad, Encoding.UTF8);

				// ParserContext fuer korrektes Namespace-Handling
				var parserContext = new ParserContext
				{
					BaseUri = new Uri(Path.GetDirectoryName(xamlDateiPfad) + Path.DirectorySeparatorChar, UriKind.Absolute)
				};

				var konfiguration = XamlReader.Parse(xamlContent, parserContext) as NeuigkeitenKonfiguration;

				if (konfiguration != null)
				{
					System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] Erfolgreich geladen: {konfiguration.Versionen.Count} Version(en)");
					foreach (var version in konfiguration.Versionen)
					{
						System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader]   Version {version.Version}: {version.Kategorien.Count} Kategorie(n)");
					}
				}

				return konfiguration ?? ErstelleStandardKonfiguration();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] FEHLER beim Laden: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] StackTrace: {ex.StackTrace}");
				return ErstelleStandardKonfiguration();
			}
		}

		/// <summary>
		/// Laedt die Neuigkeiten-Konfiguration aus einer eingebetteten Ressource
		/// </summary>
		/// <param name="resourceUri">URI zur Ressource (z.B. pack://application:,,,/...)</param>
		/// <returns>Die geladene Konfiguration oder eine leere Konfiguration bei Fehler</returns>
		public static NeuigkeitenKonfiguration LadeAusRessource(Uri resourceUri)
		{
			try
			{
				var streamInfo = Application.GetResourceStream(resourceUri);
				if (streamInfo == null)
				{
					System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] Ressource nicht gefunden: {resourceUri}");
					return ErstelleStandardKonfiguration();
				}

				using var reader = new StreamReader(streamInfo.Stream, Encoding.UTF8);
				var xamlContent = reader.ReadToEnd();
				var konfiguration = XamlReader.Parse(xamlContent) as NeuigkeitenKonfiguration;

				return konfiguration ?? ErstelleStandardKonfiguration();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] FEHLER beim Laden der Ressource: {ex.Message}");
				return ErstelleStandardKonfiguration();
			}
		}

		/// <summary>
		/// Laedt die Neuigkeiten-Konfiguration aus dem Standardpfad (Configuration/NeuigkeitenKonfiguration.xaml)
		/// </summary>
		/// <returns>Die geladene Konfiguration</returns>
		public static NeuigkeitenKonfiguration LadeStandard()
		{
			var basePath = AppDomain.CurrentDomain.BaseDirectory;
			var configPath = Path.Combine(basePath, "Configuration", "NeuigkeitenKonfiguration.xaml");

			System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] LadeStandard() - BasePath: {basePath}");
			System.Diagnostics.Debug.WriteLine($"[NeuigkeitenLoader] LadeStandard() - ConfigPath: {configPath}");

			return LadeAusDatei(configPath);
		}

		/// <summary>
		/// Erstellt eine Standard-Konfiguration als Fallback
		/// </summary>
		private static NeuigkeitenKonfiguration ErstelleStandardKonfiguration()
		{
			System.Diagnostics.Debug.WriteLine("[NeuigkeitenLoader] Verwende Standard-Konfiguration (Fallback)");

			return new NeuigkeitenKonfiguration
			{
				Titel = "Neuigkeiten",
				Versionen = new()
				{
					new VersionNeuigkeiten
					{
						Version = "1.0",
						Datum = DateTime.Now,
						Kategorien = new()
						{
							new NeuigkeitenKategorie
							{
								Titel = "Keine Konfiguration gefunden",
								Eintraege = new()
								{
									new NeuigkeitEintrag
									{
										Text = "Bitte erstellen Sie eine NeuigkeitenKonfiguration.xaml Datei",
										Beschreibung = "Die Datei sollte im Configuration-Ordner liegen"
									}
								}
							}
						}
					}
				}
			};
		}
	}
}
