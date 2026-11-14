using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreenHive.NET.Core.Common.Command;
using GreenHive.NET.Core.Common.Tutorial;

namespace GreenHive.Wpf.UI.MVVM.ViewModel.Manager
{
    public class MainWindowManager : BaseManager
    {
        private TutorialManager? _tutorialManager;

        #region Eigenschaften

        /// <summary>
        /// Statusmeldung, die im UI angezeigt werden kann.
        /// </summary>
        public string Status
        {
            get => field;
            set
            {
                if (field != value)
                {
                    field = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Initialisiert das MainWindowManager und legt die verfügbaren Befehle an.
        /// </summary>
        public MainWindowManager() : base()
        {
            InitializeTutorials();
            StartTutorialCommand = new RelayCommand(StartTutorial, CanStartTutorial);
            Status = "Anwendung bereit";

        }

        #endregion

        /// <summary>
        /// Stellt den Zugriff auf die Menü-Befehle des Fensters bereit.
        /// </summary>
        public MenuManager MenuManager
        {
            get
            {
                field ??= this.AppKontext.Produziere<MenuManager>();
                return field;
            }
            set { field = value; }
        }

        #region Tutorial-System

        public static ICommand StartTutorialCommand { get; private set; }

        /// <summary>
        /// Initialisiert das Tutorial-System mit dem MainWindow und Overlay
        /// </summary>
        /// <param name="window">Das Hauptfenster</param>
        /// <param name="overlay">Das Canvas für das Tutorial-Overlay</param>
        public void InitializeTutorialSystem(Window window, Canvas overlay)
        {
            if (window == null || overlay == null) return;

            _tutorialManager = new TutorialManager(window, overlay);

            // Event-Handler für Tutorial-Events
            _tutorialManager.StepChanged += OnTutorialStepChanged;
            _tutorialManager.TutorialCompleted += OnTutorialCompleted;
            _tutorialManager.TutorialCancelled += OnTutorialCancelled;

            Status = "Tutorial-System initialisiert";
        }

        private void InitializeTutorials()
        {
            try
            {
                // Pfad zur XML-Konfigurationsdatei
                var xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "TutorialKonfiguration.xml");

                // Fallback-Pfad falls die Datei im Hauptverzeichnis nicht gefunden wird
                if (!File.Exists(xmlFilePath))
                {
                    xmlFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "TutorialKonfiguration.xml");
                }

                if (File.Exists(xmlFilePath))
                {
                    // Tutorials aus XML laden über TutorialService
                    var tutorialCount = TutorialService.Instance.LoadTutorialsFromXml(xmlFilePath);
                    Status = $"Tutorials aus XML geladen: {tutorialCount} Tutorial(s) gefunden";
                }
                else
                {
                    // Fallback: Hardcodierte Tutorials laden
                    LoadHardcodedTutorials();
                    Status = "XML-Datei nicht gefunden, Fallback-Tutorials geladen";
                }
            }
            catch (Exception ex)
            {
                // Bei Fehlern: Fallback zu hardcodierten Tutorials
                LoadHardcodedTutorials();
                Status = $"Fehler beim Laden der XML-Tutorials: {ex.Message}. Fallback-Tutorials geladen.";
            }
        }

        /// <summary>
        /// Lädt hardcodierte Tutorials als Fallback
        /// </summary>
        private void LoadHardcodedTutorials()
        {
            var mainTutorial = new List<TutorialStep>
            {
                new() {
                    Key = "TopMenuArea",
                    Title = "Willkommen bei Voestalpine",
                    InstructionText = "Dies ist die Haupt-Navigationsleiste. Hier finden Sie alle wichtigen Funktionen der Anwendung wie Tutorial, Performance-Panel, Admin-Bereich und Hilfe.",
                    Position = TutorialPosition.Bottom,
                    CanSkip = true
                },
                new() {
                    Key = "TopMenu_Tutorial",
                    Title = "Tutorial-Funktion",
                    InstructionText = "Mit diesem Button können Sie jederzeit das Tutorial neu starten. Perfekt, um neue Features zu entdecken oder Funktionen zu wiederholen.",
                    Position = TutorialPosition.Bottom,
                    CanSkip = true
                },
                new() {
                    Key = "TopMenu_Admin",
                    Title = "Administrator-Bereich",
                    InstructionText = "Hier gelangen Sie zum Admin-Bereich, wo Sie administrative Aufgaben durchführen können.",
                    Position = TutorialPosition.Bottom,
                    CanSkip = true
                },
                new() {
                    Key = "LeftMenuArea",
                    Title = "Navigation-Menü",
                    InstructionText = "Das linke Menü bietet schnellen Zugriff auf die verschiedenen Bereiche der Anwendung. Jeder Button öffnet einen neuen Arbeitsbereich.",
                    Position = TutorialPosition.Right,
                    CanSkip = true
                },
                new() {
                    Key = "LeftMenu_Dashboard",
                    Title = "Dashboard",
                    InstructionText = "Das Dashboard bietet eine Übersicht über wichtige Informationen und Metriken Ihrer Anwendung.",
                    Position = TutorialPosition.Right,
                    CanSkip = true
                },
                new() {
                    Key = "LeftMenu_Benutzer",
                    Title = "Benutzer-Verwaltung",
                    InstructionText = "Hier können Sie Benutzerkonten verwalten, Berechtigungen zuweisen und Benutzer-Profile bearbeiten.",
                    Position = TutorialPosition.Right,
                    CanSkip = true
                },
                new() {
                    Key = "TabArea",
                    Title = "Tab-Verwaltung",
                    InstructionText = "In diesem Bereich werden alle geöffneten Ansichten als Tabs angezeigt. Sie können zwischen verschiedenen Arbeitsbereichen wechseln und mehrere Views gleichzeitig offen halten.",
                    Position = TutorialPosition.Bottom,
                    CanSkip = true
                },
                new() {
                    Key = "StatusArea",
                    Title = "Status-Information",
                    InstructionText = "Die Statusleiste zeigt wichtige Informationen über den aktuellen Zustand der Anwendung an, einschließlich Tutorial-Fortschritt und System-Status.",
                    Position = TutorialPosition.Top,
                    CanSkip = true
                }
            };

            // Erweiterte Tutorial für Menu-Navigation
            var menuTutorial = new List<TutorialStep>
            {
                new() {
                    Key = "TopMenuItems",
                    Title = "Haupt-Navigation",
                    InstructionText = "Diese Leiste enthält die wichtigsten Funktionen. Jeder Bereich hat spezifische Aufgaben und Werkzeuge.",
                    Position = TutorialPosition.Bottom,
                    CanSkip = true
                },
                new() {
                    Key = "LeftMenuItems",
                    Title = "Seiten-Navigation",
                    InstructionText = "Verwenden Sie diese Buttons, um zwischen verschiedenen Arbeitsbereichen zu wechseln. Jeder Button öffnet eine neue Ansicht im Tab-Bereich.",
                    Position = TutorialPosition.Right,
                    CanSkip = true
                }
            };

            TutorialService.Instance.RegisterTutorial("MainWindow", mainTutorial);
            TutorialService.Instance.RegisterTutorial("MenuNavigation", menuTutorial);
        }

        private void StartTutorial(object? parameter)
        {
            if (parameter is string tutorialKey && _tutorialManager != null)
            {
                if (TutorialService.Instance.GetTutorial(tutorialKey) is var steps && steps != null)
                {
                    _tutorialManager.Steps.Clear();
                    _tutorialManager.Steps.AddRange(steps);
                    _tutorialManager.Start();

                    Status = $"Tutorial '{tutorialKey}' gestartet";
                }
            }
        }

        private bool CanStartTutorial(object? parameter)
        {
            return _tutorialManager != null &&
                   !_tutorialManager.IsActive &&
                   parameter is string tutorialKey &&
                   TutorialService.Instance.HasTutorial(tutorialKey);
        }

        private void OnTutorialStepChanged(object? sender, TutorialStepEventArgs e)
        {
            Status = $"Tutorial Schritt {e.CurrentIndex + 1} von {e.TotalSteps}: {e.Step.Title}";
        }

        private void OnTutorialCompleted(object? sender, EventArgs e)
        {
            Status = "Tutorial erfolgreich abgeschlossen";
        }

        private void OnTutorialCancelled(object? sender, EventArgs e)
        {
            Status = "Tutorial abgebrochen";
        }

        /// <summary>
        /// Lädt die Tutorial-Konfiguration aus der XML-Datei neu
        /// </summary>
        /// <returns>True wenn erfolgreich geladen, false bei Fehlern</returns>
        public bool ReloadTutorialConfiguration()
        {
            try
            {
                // Aktuelle Tutorials löschen
                TutorialService.Instance.ClearTutorials();

                // Tutorials neu laden
                InitializeTutorials();

                Status = "Tutorial-Konfiguration neu geladen";
                return true;
            }
            catch (Exception ex)
            {
                Status = $"Fehler beim Neuladen der Tutorial-Konfiguration: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Gibt Informationen über die geladenen Tutorials zurück
        /// </summary>
        /// <returns>Dictionary mit Tutorial-Namen und Anzahl der Schritte</returns>
        public Dictionary<string, int> GetTutorialInfo()
        {
            var info = new Dictionary<string, int>();

            foreach (var tutorialKey in TutorialService.Instance.GetAvailableTutorials())
            {
                info[tutorialKey] = TutorialService.Instance.GetTutorialStepCount(tutorialKey);
            }

            return info;
        }

        #endregion



    }
}
