using System.Windows;
using System.Windows.Media;
using Zenox.Wpf.Controls.Common.CommandHistory;
using Zenox.Wpf.Controls.Common.CommandPalette;
using Zenox.Wpf.Core.Common.Command;
using Zenox.Wpf.Views;
using Zenox.Wpf.Views.Hilfe;

namespace Zenox.Wpf.UI.MVVM.ViewModel.Manager
{
	public class MenuManager : BaseManager
	{
		private RelayMenu _topMenu;
		private RelayMenu _pageMenu;
		private RelayMenu _bottomMenu;
		private RelayMenu _footerMenu;

		public MenuManager()
		{
			InitializeCommandPalette();
		}

		/// <summary>
		/// Initialisiert die Command Palette mit allen verfügbaren Befehlen
		/// </summary>
		private void InitializeCommandPalette()
		{
			#region Command-Palette
			var paletteManager = CommandPaletteManager.Instance;

			// Navigation Commands
			paletteManager.RegisterCommand(
				"Dashboard öffnen",
				"Navigation",
				() => this.AppKontext.TabView.LoadView<DashboardView>(),
				"Öffnet die Dashboard-Verwaltung",
				keywords: new[] { "dashboard", "übersicht", "home" }
			);

			// Command History Overlay (NEU - Hauptfeature)
			paletteManager.RegisterCommand(
				"Command-Historie anzeigen",
				"System",
				() => CommandHistoryManager.Instance.ShowHistory(),
				"Zeigt die Historie aller ausgeführten Commands (Live-Tracking)",
				"Ctrl+Shift+H",
				keywords: new[] { "command", "history", "historie", "verlauf", "tracking", "log", "aufzeichnung" }
			);

			// Einstellungen Commands
			paletteManager.RegisterCommand(
				"Einstellungen öffnen",
				"Einstellungen",
				() => this.AppKontext.TabView.LoadView<EinstellungenView>(),
				"Öffnet die Anwendungseinstellungen inkl. Snapshot-Konfiguration",
				keywords: new[] { "einstellungen", "settings", "konfiguration", "snapshot" }
			);

			paletteManager.RegisterCommand(
				"Performancepanel öffnen",
				"Einstellungen",
				() =>
				{
					Controls.Common.ExecutionLog.ExecutionLogFenster exelogWindow = new();
					exelogWindow.ExecutionLogList = CommandPool.ExecutionLog;
					exelogWindow.Show();
				},
				"Zeigt das Performance-Monitoring-Panel mit Telemetrie",
				"F11",
				keywords: new[] { "performance", "monitoring", "log", "geschwindigkeit", "telemetrie", "metriken" }
			);

			// Hilfe Commands
			paletteManager.RegisterCommand(
				"Hilfe anzeigen",
				"Hilfe",
				() => MainWindowManager.StartTutorialCommand.Execute("MainWindow"),
				"Startet das interaktive Tutorial",
				"Ctrl+F1",
				keywords: new[] { "hilfe", "tutorial", "help", "anleitung" }
			);

			paletteManager.RegisterCommand(
				"Neuigkeiten anzeigen",
				"Hilfe",
				() => this.AppKontext.TabView.LoadView<NeuigkeitenView>(),
				"Zeigt die neuesten Updates und Features",
				keywords: new[] { "neuigkeiten", "news", "updates", "changelog" }
			);

			paletteManager.RegisterCommand(
				"About anzeigen",
				"Hilfe",
				() => this.AppKontext.TabView.LoadView<AboutView>(),
				"Zeigt Informationen über die Anwendung",
				keywords: new[] { "about", "über", "info", "version" }
			);

			// System Commands
			paletteManager.RegisterCommand(
				"Anwendungs-Logs",
				"System",
				() => MessageBox.Show("Anwendungs-Logs!"),
				"Zeigt die Anwendungs-Logs",
				keywords: new[] { "logs", "protokoll", "events" }
			);

			// Background Tasks Commands
			paletteManager.RegisterCommand(
				"Background Tasks anzeigen",
				"System",
				() =>
				{
					var window = new Controls.Common.ExecutionLog.ExecutionLogFenster();
					window.ExecutionLogList = CommandPool.ExecutionLog;
					window.Show();
				},
				"Zeigt alle laufenden Hintergrundaufgaben",
				keywords: new[] { "tasks", "background", "jobs", "scheduler" }
			);

			// Notification System Commands
			paletteManager.RegisterCommand(
				"Test-Benachrichtigung (Info)",
				"System",
				() => Core.Common.Notifications.NotificationService.Instance.ShowInfo(
					"Test Info",
					"Dies ist eine Test-Benachrichtigung vom Typ Info"),
				"Zeigt eine Test-Info-Benachrichtigung an",
				keywords: new[] { "notification", "benachrichtigung", "toast", "info", "test" }
			);

			paletteManager.RegisterCommand(
				"Test-Benachrichtigung (Erfolg)",
				"System",
				() => Core.Common.Notifications.NotificationService.Instance.ShowSuccess(
					"Erfolg!",
					"Operation wurde erfolgreich abgeschlossen"),
				"Zeigt eine Test-Erfolgs-Benachrichtigung an",
				keywords: new[] { "notification", "benachrichtigung", "toast", "success", "erfolg", "test" }
			);

			paletteManager.RegisterCommand(
				"Test-Benachrichtigung (Warnung)",
				"System",
				() => Core.Common.Notifications.NotificationService.Instance.ShowWarning(
					"Warnung",
					"Dies ist eine Warnung, die Ihre Aufmerksamkeit erfordert"),
				"Zeigt eine Test-Warnungs-Benachrichtigung an",
				keywords: new[] { "notification", "benachrichtigung", "toast", "warning", "warnung", "test" }
			);

			paletteManager.RegisterCommand(
				"Test-Benachrichtigung (Fehler)",
				"System",
				() => Core.Common.Notifications.NotificationService.Instance.ShowError(
					"Fehler!",
					"Ein Fehler ist aufgetreten"),
				"Zeigt eine Test-Fehler-Benachrichtigung an",
				keywords: new[] { "notification", "benachrichtigung", "toast", "error", "fehler", "test" }
			);

			paletteManager.RegisterCommand(
				"Alle Benachrichtigungen löschen",
				"System",
				() => Core.Common.Notifications.NotificationService.Instance.Clear(),
				"Löscht alle aktiven Benachrichtigungen",
				keywords: new[] { "notification", "benachrichtigung", "clear", "löschen", "entfernen" }
			);

			// Update System Commands
			paletteManager.RegisterCommand(
				"Nach Updates suchen",
				"System",
				async () =>
				{
					Core.Common.Notifications.NotificationService.Instance.ShowInfo(
						"Update-Prüfung",
						"Suche nach verfügbaren Updates...");

					var updateInfo = await Core.Common.Updates.AutoUpdateService.Instance.CheckForUpdatesAsync();

					if (updateInfo != null)
					{
						var dialog = new Controls.Common.Updates.UpdateNotificationDialog(updateInfo);
						dialog.ShowDialog();
					}
					else
					{
						Core.Common.Notifications.NotificationService.Instance.ShowSuccess(
							"Keine Updates",
							"Sie verwenden bereits die neueste Version");
					}
				},
				"Prüft auf verfügbare Software-Updates",
				keywords: new[] { "update", "aktualisierung", "version", "upgrade" }
			);

			// Recovery Commands
			paletteManager.RegisterCommand(
				"App-State speichern",
				"System",
				() =>
				{
					Core.Common.Recovery.CrashRecoveryManager.Instance.SaveSnapshot(false);
					Core.Common.Notifications.NotificationService.Instance.ShowSuccess(
						"State gespeichert",
						"Aktueller Anwendungs-State wurde gesichert");
				},
				"Speichert den aktuellen Anwendungs-State",
				keywords: new[] { "state", "speichern", "snapshot", "backup", "recovery" }
			);

			paletteManager.RegisterCommand(
				"App-State löschen",
				"System",
				() =>
				{
					Core.Common.Recovery.CrashRecoveryManager.Instance.ClearState();
					Core.Common.Notifications.NotificationService.Instance.ShowInfo(
						"State gelöscht",
						"Gespeicherter Anwendungs-State wurde entfernt");
				},
				"Löscht den gespeicherten Anwendungs-State",
				keywords: new[] { "state", "löschen", "clear", "recovery" }
			);

			// Snapshot System Commands - jetzt über Einstellungen
			paletteManager.RegisterCommand(
				"Snapshot-Einstellungen öffnen",
				"Power-User",
				() => this.AppKontext.TabView.LoadView<EinstellungenView>(),
				"Öffnet die Einstellungen für das Snapshot-System",
				keywords: new[] { "snapshot", "state", "backup", "restore", "wiederherstellen", "sicherung", "einstellungen" }
			);

			paletteManager.RegisterCommand(
				"Neuen Snapshot erstellen",
				"Power-User",
				() =>
				{
					Core.Common.Snapshots.SnapshotManager.Instance.CreateAllCategorySnapshots("Manuell");
					Core.Common.Notifications.NotificationService.Instance.ShowSuccess(
						"Snapshot erstellt",
						"Neuer Snapshot wurde erfolgreich erstellt");
				},
				"Erstellt einen neuen Snapshot des aktuellen Zustands",
				keywords: new[] { "snapshot", "create", "erstellen", "backup", "save" }
			);

			// Power-User Mode Commands (NEW)
			paletteManager.RegisterCommand(
				"Power-User Modus umschalten",
				"Power-User",
				() =>
				{
					Core.Common.PowerUser.PowerUserMode.Instance.Toggle();
					var isEnabled = Core.Common.PowerUser.PowerUserMode.Instance.IsEnabled;
					Core.Common.Notifications.NotificationService.Instance.ShowInfo(
						"Power-User Modus",
						isEnabled ? "Power-User Modus aktiviert" : "Power-User Modus deaktiviert");
				},
				"Schaltet den Power-User Modus ein/aus",
				"Ctrl+Shift+P",
				keywords: new[] { "power", "user", "advanced", "expert", "modus", "mode" }
			);

			paletteManager.RegisterCommand(
				"Performance-Overlay anzeigen",
				"Power-User",
				() =>
				{
					if (!Core.Common.PowerUser.PowerUserMode.Instance.IsFeatureEnabled(
						Core.Common.PowerUser.PowerUserFeature.PerformanceOverlay))
					{
						Core.Common.Notifications.NotificationService.Instance.ShowWarning(
							"Feature nicht verfügbar",
							"Aktivieren Sie zuerst den Power-User Modus");
						return;
					}
					// PerformanceOverlay ist ein UserControl, nicht ein Window - verwende ExecutionLog stattdessen
					var window = new Controls.Common.ExecutionLog.ExecutionLogFenster();
					window.ExecutionLogList = CommandPool.ExecutionLog;
					window.Show();
				},
				"Zeigt das Performance-Overlay an",
				keywords: new[] { "performance", "overlay", "monitoring", "fps", "memory" }
			);

			// Command Replay Commands (NEW)
			paletteManager.RegisterCommand(
				"Befehlsverlauf öffnen",
				"Power-User",
				() =>
				{
					var window = new Controls.Common.CommandReplay.CommandHistoryWindow();
					window.Show();
				},
				"Öffnet den Befehlsverlauf für Wiedergabe und Automatisierung",
				keywords: new[] { "command", "befehl", "history", "verlauf", "replay", "wiedergabe" }
			);

			paletteManager.RegisterCommand(
				"Befehlsaufzeichnung umschalten",
				"Power-User",
				() =>
				{
					Core.Common.CommandReplay.CommandReplayManager.Instance.IsRecording =
						!Core.Common.CommandReplay.CommandReplayManager.Instance.IsRecording;
					var isRecording = Core.Common.CommandReplay.CommandReplayManager.Instance.IsRecording;
					Core.Common.Notifications.NotificationService.Instance.ShowInfo(
						"Befehlsaufzeichnung",
						isRecording ? "Aufzeichnung aktiviert" : "Aufzeichnung deaktiviert");
				},
				"Startet/Stoppt die Aufzeichnung von Befehlen",
				keywords: new[] { "recording", "aufzeichnung", "command", "befehl", "capture" }
			);

			// Command Palette selbst
			paletteManager.RegisterCommand(
				"Befehlspalette öffnen",
				"System",
				() => CommandPaletteManager.Instance.ShowPalette(),
				"Öffnet die Befehlspalette",
				"Ctrl+K",
				keywords: new[] { "befehl", "command", "palette", "suche", "search" }
			);

			#endregion
		}

		public RelayMenu TopMenu
		{
			get
			{
				if (_topMenu == null)
				{
					_topMenu = new RelayMenu();

					_topMenu.Add(new RelayObject
					{
						Name = "Einstellungen",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
							this.AppKontext.TabView.LoadView<EinstellungenView>();
						}, _ => true),
						Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
							new() {
								Name = "Performancepanel",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									Controls.Common.ExecutionLog.ExecutionLogFenster exelogWindow = new();
									exelogWindow.ExecutionLogList = CommandPool.ExecutionLog;
									exelogWindow.Show();
								}, _ => true)
							},
							new() {
								Name = "Anzeigeeinstellungen",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
								}, _ => true)
							},
							new() {
								Name = "Anwendungseinstellungen",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									this.AppKontext.TabView.LoadView<EinstellungenView>();
								}, _ => true)
							},

						}
					});

					_topMenu.Add(new RelayObject
					{
						Name = "Benutzer",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
						}, _ => true),
						Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
							new() {

								Name = "Profil",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
								}, _ => true)
							},
							new() {

								Name = "An/Abmelden",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
								}, _ => true)
							},

						}
					});

					_topMenu.Add(new RelayObject
					{
						Name = "Hilfe",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
						}, _ => true),
						Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
							new() {

								Name = "Hilfe Anzeigen",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									MainWindowManager.StartTutorialCommand.Execute("MainWindow");
								}, _ => true)
							},
							new() {

								Name = "Glossar",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
								}, _ => true)
							},
							new() {

								Name = "Neuigkeiten",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									this.AppKontext.TabView.LoadView<NeuigkeitenView>();
								}, _ => true)
							},
							new() {

								Name = "Feedback senden",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
								}, _ => true),
								Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject> {
									new() {

										Name = "Problem melden",
										Foreground = Brushes.White,
										Command = new RelayCommand(
										_ =>
										{
										}, _ => true)
									},
									new() {

										Name = "Feature vorschlagen",
										Foreground = Brushes.White,
										Command = new RelayCommand(
										_ =>
										{
										}, _ => true)
									}
								},
							},
							new() {

								Name = "About",
								Foreground = Brushes.White,
								Command = new RelayCommand(
								_ =>
								{
									this.AppKontext.TabView.LoadView<AboutView>();
								}, _ => true)
							},

						}
					});

				}
				return _topMenu;
			}
		}

		public RelayMenu PageMenu
		{
			get
			{
				if (_pageMenu == null)
				{
					_pageMenu = new RelayMenu();

					// Dashboard
					_pageMenu.Add(new RelayObject
					{
						PngPath = "/Assets/arrow-right-02-stroke-rounded.png",
						Name = "Dashboard",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
							this.AppKontext.TabView.LoadView<DashboardView>();
						}, _ => true)
					});
				}
				return _pageMenu;
			}
		}

		public RelayMenu BottomMenu
		{
			get
			{
				if (_bottomMenu == null)
				{
					_bottomMenu = new RelayMenu();
					_bottomMenu.Add(new RelayObject
					{
						Name = "Anwendungs-Logs",
						Foreground = Brushes.White,
						Command = new RelayCommand(
						_ =>
						{
							MessageBox.Show("Anwendungs-Logs!");
						}, _ => true)
					});
				}
				return _bottomMenu;
			}
		}

		public RelayMenu FooterMenü
		{
			get
			{
				if (_footerMenu == null)
				{
					var relayCommandPool = new RelayCommandPool();
					// Footer-Menu Implementation falls benötigt
				}
				return _footerMenu;
			}
			set { SetProperty(ref _footerMenu, value); }
		}
	}
}
