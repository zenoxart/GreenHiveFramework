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

namespace Zenox.Wpf.UI;

public partial class App : Application
{
	protected CompositeModuleHandler ModuleHandler { get; set; } = null!;

	private void Application_Startup(object sender, StartupEventArgs e)
	{
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
		CrashRecoveryManager.Instance.Initialize();

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

	protected override void OnExit(ExitEventArgs e)
	{
		Settings.Default.LocalisationISO = this.ModuleHandler.AppKontext.Sprachen.AktuelleSprache.Code;
		Settings.Default.Save();

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
}

