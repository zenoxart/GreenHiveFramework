using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GreenHive.NET.Core.Common.Tutorial
{
    /// <summary>
    /// Definiert einen Service für die Tutorial-Engine
    /// </summary>
    public class TutorialService
    {
        private static TutorialService? _instance;
        public static TutorialService Instance => _instance ??= new TutorialService();

        private readonly Dictionary<string, List<TutorialStep>> _tutorials = new();
        private TutorialManager? _currentManager;

        /// <summary>
        /// Legt ein neues Tutorial an
        /// </summary>
        public void RegisterTutorial(string key, List<TutorialStep> steps)
        {
            _tutorials[key] = steps;
        }

        /// <summary>
        /// Läd ein gewisses Tutorial nach dem übergebenen Key
        /// </summary>
        public List<TutorialStep>? GetTutorial(string tutorialKey)
        {
            return _tutorials.TryGetValue(tutorialKey, out var steps) ? steps : null;
        }

        /// <summary>
        /// Überprüft, ob das gewollte Tutorial vorhanden ist
        /// </summary>
        /// <param name="tutorialKey"></param>
        /// <returns></returns>
        public bool HasTutorial(string tutorialKey)
        {
            return _tutorials.ContainsKey(tutorialKey);
        }

        /// <summary>
        /// Lädt Tutorials aus einer XML-Datei und registriert sie
        /// </summary>
        /// <param name="xmlFilePath">Pfad zur XML-Datei</param>
        /// <returns>Anzahl der geladenen Tutorials</returns>
        public int LoadTutorialsFromXml(string xmlFilePath)
        {
            if (!File.Exists(xmlFilePath))
            {
                throw new FileNotFoundException($"Tutorial-XML-Datei nicht gefunden: {xmlFilePath}");
            }

            var tutorials = TutorialXmlLoader.LoadTutorialsFromXml(xmlFilePath);

            foreach (var tutorial in tutorials)
            {
                RegisterTutorial(tutorial.Key, tutorial.Value);
            }

            return tutorials.Count;
        }

        /// <summary>
        /// Lädt Tutorials aus einem XML-String und registriert sie
        /// </summary>
        /// <param name="xmlContent">XML-Inhalt als String</param>
        /// <returns>Anzahl der geladenen Tutorials</returns>
        public int LoadTutorialsFromXmlString(string xmlContent)
        {
            var tutorials = TutorialXmlLoader.LoadTutorialsFromXmlString(xmlContent);

            foreach (var tutorial in tutorials)
            {
                RegisterTutorial(tutorial.Key, tutorial.Value);
            }

            return tutorials.Count;
        }

        /// <summary>
        /// Löscht alle registrierten Tutorials
        /// </summary>
        public void ClearTutorials()
        {
            _tutorials.Clear();
        }

        /// <summary>
        /// Startet das Tutorial
        /// </summary>
        /// <param name="tutorialKey">Zu startendes Tutorial</param>
        /// <param name="window">Anwendungsfenster</param>
        /// <param name="overlay">Canvas zum Ausgrauen der nicht relevanten Bereiche</param>
        /// <exception cref="ArgumentException"></exception>
        public void StartTutorial(string tutorialKey, System.Windows.Window window, Canvas overlay)
        {
            if (!_tutorials.TryGetValue(tutorialKey, out var steps))
            {
                throw new ArgumentException($"Tutorial '{tutorialKey}' nicht gefunden.");
            }

            _currentManager?.Dispose();
            _currentManager = new TutorialManager(window, overlay);
            _currentManager.Steps.AddRange(steps);
            _currentManager.Start();
        }

        /// <summary>
        /// Stoppt das aktuelle Tutorial
        /// </summary>
        public void StopCurrentTutorial()
        {
            _currentManager?.Dispose();
            _currentManager = null;
        }

        public bool IsActive => _currentManager?.IsActive ?? false;

        /// <summary>
        /// Gibt eine Liste der Namen der verfügbaren Tutorials
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetAvailableTutorials()
        {
            return _tutorials.Keys;
        }

        /// <summary>
        /// Gibt die Anzahl der geladenen Tutorial-Schritte für ein Tutorial zurück
        /// </summary>
        /// <param name="tutorialKey">Tutorial-Schlüssel</param>
        /// <returns>Anzahl der Schritte oder 0 wenn nicht gefunden</returns>
        public int GetTutorialStepCount(string tutorialKey)
        {
            return _tutorials.TryGetValue(tutorialKey, out var steps) ? steps.Count : 0;
        }
    }
}