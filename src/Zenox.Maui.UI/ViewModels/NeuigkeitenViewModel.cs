using Zenox.NET.Core.Common.Neuigkeiten;
using Zenox.NET.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Maui.UI.ViewModels
{
    /// <summary>
    /// ViewModel für die Neuigkeiten-Ansicht
    /// </summary>
    public class NeuigkeitenViewModel : NotificationObject
    {
        private NeuigkeitenKonfiguration? _konfiguration;
        private VersionNeuigkeiten? _ausgewaehlteVersion;
        private bool _istLadevorgang;

        /// <summary>
        /// Die geladene Neuigkeiten-Konfiguration
        /// </summary>
        public NeuigkeitenKonfiguration? Konfiguration
        {
            get => _konfiguration;
            set
            {
                _konfiguration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HatVersionen));
                
                // Automatisch erste Version auswählen
                if (value?.Versionen.Any() == true)
                {
                    AusgewaehlteVersion = value.Versionen.First();
                }
            }
        }

        /// <summary>
        /// Die aktuell ausgewählte Version
        /// </summary>
        public VersionNeuigkeiten? AusgewaehlteVersion
        {
            get => _ausgewaehlteVersion;
            set
            {
                _ausgewaehlteVersion = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gibt an, ob gerade ein Ladevorgang läuft
        /// </summary>
        public bool IstLadevorgang
        {
            get => _istLadevorgang;
            set
            {
                _istLadevorgang = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gibt an, ob Versionen vorhanden sind
        /// </summary>
        public bool HatVersionen => Konfiguration?.Versionen.Any() == true;

        public NeuigkeitenViewModel()
        {
            LadeNeuigkeiten();
        }

        /// <summary>
        /// Lädt die Neuigkeiten aus der Konfiguration
        /// </summary>
        public async void LadeNeuigkeiten()
        {
            IstLadevorgang = true;

            try
            {
                var config = await Task.Run(() =>
                {
                    // Versuche erst Standard-Konfiguration zu laden
                    var cfg = NeuigkeitenLoader.LadeStandard();
                    
                    // Wenn keine gefunden, verwende Fallback
                    if (cfg.Versionen.Count == 0)
                    {
                        cfg = NeuigkeitenLoader.ErstelleStandardKonfiguration();
                    }

                    return cfg;
                });

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Konfiguration = config;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NeuigkeitenViewModel] Fehler beim Laden: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Konfiguration = NeuigkeitenLoader.ErstelleStandardKonfiguration();
                });
            }
            finally
            {
                IstLadevorgang = false;
            }
        }

        /// <summary>
        /// Aktualisiert die Neuigkeiten
        /// </summary>
        public void Aktualisieren()
        {
            LadeNeuigkeiten();
        }
    }
}
