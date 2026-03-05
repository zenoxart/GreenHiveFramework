using System.Windows;
using Zenox.Wpf.Controls.Common.CommandPalette;
using Zenox.NET.Core.Common.BackgroundTasks;
using Zenox.NET.Core.Common.Heatmap;
using Zenox.NET.Core.Common.Telemetry;
using Zenox.NET.Core.Common.Updates;
using Zenox.Wpf.Advanced.MVVM.Composite;


using Zenox.Wpf.UI.Common;
using Zenox.Wpf.UI.MVVM.ViewModel;
using Zenox.Wpf.UI.Properties;
using Zenox.NET.Core.Common.Recovery;
using Zenox.NET.Core.Common.Notifications;
using Zenox.Wpf.UI.Common.Shortcuts;
using System.Windows.Input;

namespace Zenox.Wpf.UI;

public partial class App : Application
{
	protected CompositeModuleHandler ModuleHandler { get; set; } = null!;

	private void Application_Startup(object sender, StartupEventArgs e)
	{
		// Window Manager Service initialisieren
		Zenox.Wpf.Views.Helpers.WindowManagerServiceLocator.Instance = new Services.WindowManagerService();

		// Language Service initialisieren
		Zenox.Wpf.Views.Helpers.LanguageServiceLocator.Instance = new Services.LanguageService();
		
		// Lokalisierung initialisieren
		Common.Localization.LocalizationManager.Instance.Initialize();

		// Shortcut Manager initialisieren und konfigurieren
		InitializeShortcutManager();

		// Background Task Scheduler starten
		BackgroundTaskScheduler.Instance.Start();

		// Telemetrie-Service initialisieren
		TelemetryService.Instance.Initialize();

		// Interaction Heatmap Controller initialisieren und starten
		InteractionHeatmapController.Instance.Initialize(new HeatmapConfiguration
		{
			TrackClicks = true,
			TrackHover = true,
			TrackScroll = false,
			MinHoverDurationMs = 500,
			MaxInteractions = 10000
		});
		InteractionHeatmapController.Instance.StartTracking();

		// Crash Recovery Manager initialisieren
		//CrashRecoveryManager.Instance.Initialize();

		// Check for previous crash
		var crashSnapshot = CrashRecoveryManager.Instance.GetLastCrashSnapshot();
		if (crashSnapshot != null)
		{
			var crashDialog = new Controls.Common.Recovery.CrashRecoveryDialog(crashSnapshot);
			if (crashDialog.ShowDialog() == true && crashDialog.ShouldRestore)
			{
				CrashRecoveryManager.Instance.RestoreLastState();
				NotificationService.Instance.ShowSuccess(
					"Wiederherstellung erfolgreich",
					"Die letzte Sitzung wurde wiederhergestellt");
			}
		}


		// Auto-Update Service initialisieren (optional - URL konfigurieren!)
		// AutoUpdateService.Instance.Initialize("https://your-server.com/updates/manifest.json");
		AutoUpdateService.Instance.UpdateAvailable += (s, versionInfo) =>
		{
			Dispatcher.Invoke(() =>
			{
				NotificationService.Instance.ShowInfo(
					"Update verfügbar",
					$"Version {versionInfo.Version} ist jetzt verfügbar!",
					8,
					() =>
					{
						var dialog = new Controls.Common.Updates.UpdateNotificationDialog(versionInfo);
						dialog.ShowDialog();
					});
			});
		};

		var uiAppKontext = new UIAppKontext();
		ModuleHandler = new CompositeModuleHandler(uiAppKontext);
		//ModuleHandler.AppKontext.Sprachen.Festlegen(Settings.Default.LocalisationISO);

		var Shell = new MainAppShell();
		Shell.DataContext = ModuleHandler.AppKontext;
		
		// Window Manager initialisieren und Main Shell tracken
		Zenox.Wpf.UI.Common.WindowManager.Instance.TrackWindow(Shell, "MainAppShell");


		
		Shell.Show();

		// Prüfe ob Benutzer angemeldet ist
		var savedToken = Settings.Default.AuthToken;
		
		// DEBUG: Token temporär zurücksetzen für Tests (kann später entfernt werden)
		// Settings.Default.AuthToken = string.Empty;
		// Settings.Default.Save();
		
		if (string.IsNullOrEmpty(savedToken))
		{
			// Zeige Login-View
			ShowLoginView(Shell, uiAppKontext);
		}
		else
		{
			// TODO: Token-Validierung gegen API durchführen
			// Falls Token ungültig, zur Login-View wechseln
			
			// Lade Hauptanwendung
			ModuleHandler.LoadView<MainWindow, AppViewModel>("MainView", Shell.MainModule);
		}

		TelemetryService.Instance.LogEvent("ApplicationStarted", "System");

		// Auto-Save State periodisch (alle 5 Minuten)
		BackgroundTaskScheduler.Instance.ScheduleRecurringTask(
			"Auto-Save State",
			async (ct) =>
			{
				await System.Threading.Tasks.Task.Run(() =>
					CrashRecoveryManager.Instance.SaveSnapshot(false), ct);
			},
			System.TimeSpan.FromMinutes(5),
			TaskPriority.Low);
	}

	private void ShowLoginView(MainAppShell shell, UIAppKontext appKontext)
	{
		var authView = new Views.AuthView();
		var authViewModel = authView.DataContext as MVVM.ViewModel.AuthViewModel;
		
		if (authViewModel != null)
		{
			authViewModel.AuthenticationSuccessful += (s, args) =>
			{
				// Token speichern
				if (args.Token != null)
				{
					Settings.Default.AuthToken = args.Token;
					Settings.Default.Save();
				}

				// Hauptanwendung laden
				Dispatcher.Invoke(() =>
				{
					ModuleHandler.LoadView<MainWindow, AppViewModel>("MainView", shell.MainModule);
					
					NotificationService.Instance.ShowSuccess(
						"Anmeldung erfolgreich",
						$"Willkommen zurück, {args.User?.Username}!");
				});
			};
		}

		shell.MainModule.Content = authView;
	}

	private void InitializeShortcutManager()
	{
		// Shortcuts laden
		ShortcutManager.Instance.LoadConfiguration();

		// Standard-Shortcuts registrieren (werden später über ModuleHandler verfügbar gemacht)
		ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
			"tutorial_main",
			"Hauptfenster-Tutorial starten",
			new KeyGesture(Key.F1),
			() => 
			{
				// Tutorial wird über MainWindow.xaml.cs KeyDown Handler ausgeführt
			},
			"Tutorial",
			"Startet das Tutorial für das Hauptfenster"));

		ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
			"tutorial_menu",
			"Menü-Navigation-Tutorial starten",
			new KeyGesture(Key.F2),
			() => 
			{
				// Tutorial wird über MainWindow.xaml.cs KeyDown Handler ausgeführt
			},
			"Tutorial",
			"Startet das Tutorial für die Menü-Navigation"));

		ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
			"save",
			"Speichern",
			new KeyGesture(Key.S, ModifierKeys.Control),
			() => NotificationService.Instance.ShowInfo("Speichern", "Speichern-Funktion ausgeführt"),
			"Datei",
			"Speichert die aktuelle Datei"));

		ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
			"open_settings",
			"Einstellungen öffnen",
			new KeyGesture(Key.OemComma, ModifierKeys.Control),
			() => OpenShortcutSettings(),
			"Navigation",
			"Öffnet die Einstellungen"));

		ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
			"quit",
			"Anwendung beenden",
			new KeyGesture(Key.Q, ModifierKeys.Control),
			() => Application.Current.Shutdown(),
			"Anwendung",
			"Beendet die Anwendung"));

		// Debug: Lokalisierungs-Scanner
		#if DEBUG
		ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
			"scan_localization",
			"Lokalisierungs-Scanner",
			new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Shift),
			() => Common.Localization.LocalizationScanner.ShowScanDialog(),
			"Debug",
			"Scannt die Anwendung nach hardcodierten Strings"));
		#endif
	}


	private void OpenShortcutSettings()
	{
		var settingsWindow = new Window
		{
			Title = "Tastenkürzel verwalten",
			Width = 900,
			Height = 600,
			Content = new Views.ShortcutManagerView(),
			WindowStartupLocation = WindowStartupLocation.CenterScreen
		};
		settingsWindow.ShowDialog();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		var aktuelleSprache = this.ModuleHandler?.AppKontext?.Sprachen?.AktuelleSprache;
		if (aktuelleSprache != null)
		{
			Settings.Default.LocalisationISO = aktuelleSprache.Code;
			Settings.Default.Save();
		}

		// Save Shortcuts
		ShortcutManager.Instance.SaveConfiguration();

		// Stop Heatmap Tracking
		InteractionHeatmapController.Instance.StopTracking();

		// Save final state
		CrashRecoveryManager.Instance.SaveSnapshot(false);

		_ = BackgroundTaskScheduler.Instance.StopAsync();
		// TODO: Implement GlobalKeyboardHandler
		// GlobalKeyboardHandler.Shutdown();
		AutoUpdateService.Instance.StopAutoCheck();

		TelemetryService.Instance.LogEvent("ApplicationExit", "System");

		base.OnExit(e);
	}

	/// <summary>
	/// Initialisiert die Lokalisierung mit ausführlichem Logging
	/// </summary>
	private void InitializeLocalization()
	{
		System.Diagnostics.Debug.WriteLine("========================================");
		System.Diagnostics.Debug.WriteLine("=== LOKALISIERUNG STARTEN ===");
		System.Diagnostics.Debug.WriteLine("========================================");

		try
		{
			// Language Service initialisieren
			Zenox.Wpf.Views.Helpers.LanguageServiceLocator.Instance = new Services.LanguageService();
			System.Diagnostics.Debug.WriteLine("✓ LanguageService bereit");
			
			// Lokalisierung initialisieren
			Common.Localization.LocalizationManager.Instance.Initialize();
			System.Diagnostics.Debug.WriteLine("✓ LocalizationManager initialisiert");

			// Diagnostik im Debug-Modus
			#if DEBUG
			Common.Localization.LocalizationDiagnostics.RunDiagnostics();
			#endif

			System.Diagnostics.Debug.WriteLine("========================================");
			System.Diagnostics.Debug.WriteLine("=== LOKALISIERUNG ERFOLGREICH ===");
			System.Diagnostics.Debug.WriteLine("========================================");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"✗ FEHLER bei Lokalisierung: {ex.Message}");
			System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
			
			MessageBox.Show(
				$"Die Spracheinstellungen konnten nicht geladen werden:\n\n{ex.Message}\n\n" +
				$"Die Anwendung wird mit Standardeinstellungen fortgesetzt.\n\n" +
				$"Tipp: Führen Sie 'Scripts\\ResetLanguageSettings.bat' aus, um die Einstellungen zurückzusetzen.",
				"Lokalisierungs-Warnung",
				MessageBoxButton.OK,
				MessageBoxImage.Warning);
		}
	}
}


