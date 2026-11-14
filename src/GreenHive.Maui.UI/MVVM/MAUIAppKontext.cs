using GreenHive.NET.Core.Common.MVVM.FactoryInjection;

namespace GreenHive.Maui.UI.MVVM
{
    /// <summary>
    /// Anwendungskontext für MAUI mit zentraler Verwaltung von Services und Managern
    /// </summary>
    public class MAUIAppKontext : AppKontext
    {
        private static MAUIAppKontext? _instance;
        private MenuManager? _menuManager;

        /// <summary>
        /// Singleton-Instanz des AppKontext
        /// </summary>
        public static MAUIAppKontext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MAUIAppKontext();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Manager für die Navigation und Menüstruktur
        /// </summary>
        public MenuManager MenuManager
        {
            get
            {
                if (_menuManager == null)
                {
                    _menuManager = new MenuManager();
                }
                return _menuManager;
            }
        }

        private MAUIAppKontext()
        {
            Initialize();
        }

        /// <summary>
        /// Initialisiert den AppKontext mit Standard-Services
        /// </summary>
        private void Initialize()
        {
            // Hier können weitere Services initialisiert werden
            System.Diagnostics.Debug.WriteLine("MAUIAppKontext initialisiert");
        }

        /// <summary>
        /// Gibt alle Ressourcen frei
        /// </summary>
        public void Dispose()
        {
            _menuManager = null;
            _instance = null;
        }
    }
}
