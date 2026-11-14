namespace GreenHive.Wpf.Controls.Common.CommandHistory
{
    /// <summary>
    /// Manager fuer das Command History Overlay - Zentrale Verwaltung und Anzeige
    /// </summary>
    public class CommandHistoryManager
    {
        private static CommandHistoryManager? _instance;
        private CommandHistoryOverlay? _overlayWindow;
        private bool _isOpening;

        private CommandHistoryManager()
        {
        }

        /// <summary>
        /// Singleton-Instanz des Managers
        /// </summary>
        public static CommandHistoryManager Instance => _instance ??= new CommandHistoryManager();

        /// <summary>
        /// Zeigt das Command History Overlay an
        /// </summary>
        public void ShowHistory()
        {
            // Verhindere mehrfaches Öffnen
            if (_isOpening)
                return;

            // Wenn bereits offen und sichtbar, Fokus setzen
            if (_overlayWindow != null && _overlayWindow.IsVisible)
            {
                _overlayWindow.Activate();
                return;
            }

            // Altes Fenster aufräumen falls vorhanden
            if (_overlayWindow != null)
            {
                _overlayWindow = null;
            }

            _isOpening = true;
            try
            {
                _overlayWindow = new CommandHistoryOverlay();
                _overlayWindow.Closed += (s, e) =>
                {
                    _overlayWindow = null;
                };
                _overlayWindow.Show();
            }
            finally
            {
                _isOpening = false;
            }
        }

        /// <summary>
        /// Schliesst das Command History Overlay
        /// </summary>
        public void HideHistory()
        {
            if (_overlayWindow != null && _overlayWindow.IsVisible)
            {
                try
                {
                    _overlayWindow.Close();
                }
                catch (InvalidOperationException)
                {
                    // Bereits geschlossen - ignorieren
                }
            }
            _overlayWindow = null;
        }

        /// <summary>
        /// Gibt an, ob das Overlay aktuell sichtbar ist
        /// </summary>
        public bool IsVisible => _overlayWindow?.IsVisible ?? false;

        /// <summary>
        /// Toggle - zeigt oder versteckt das Overlay
        /// </summary>
        public void Toggle()
        {
            if (IsVisible)
                HideHistory();
            else
                ShowHistory();
        }
    }
}
