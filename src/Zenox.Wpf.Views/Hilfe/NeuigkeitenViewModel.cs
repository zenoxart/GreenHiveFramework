using Zenox.Wpf.Core.Common.Neuigkeiten;

namespace Zenox.Wpf.Views.Hilfe
{
	/// <summary>
	/// ViewModel für die NeuigkeitenView
	/// </summary>
	public class NeuigkeitenViewModel
	{
		/// <summary>
		/// Die geladene Neuigkeiten-Konfiguration
		/// </summary>
		public NeuigkeitenKonfiguration Konfiguration { get; }

		/// <summary>
		/// Erstellt ein neues NeuigkeitenViewModel und lädt die Konfiguration
		/// </summary>
		public NeuigkeitenViewModel()
		{
			Konfiguration = NeuigkeitenLoader.LadeStandard();
		}

		/// <summary>
		/// Erstellt ein neues NeuigkeitenViewModel mit einer benutzerdefinierten Konfiguration
		/// </summary>
		/// <param name="konfiguration">Die zu verwendende Konfiguration</param>
		public NeuigkeitenViewModel(NeuigkeitenKonfiguration konfiguration)
		{
			Konfiguration = konfiguration;
		}

		/// <summary>
		/// Erstellt ein neues NeuigkeitenViewModel und lädt die Konfiguration aus dem angegebenen Pfad
		/// </summary>
		/// <param name="konfigurationsPfad">Pfad zur XAML-Konfigurationsdatei</param>
		public NeuigkeitenViewModel(string konfigurationsPfad)
		{
			Konfiguration = NeuigkeitenLoader.LadeAusDatei(konfigurationsPfad);
		}
	}
}
