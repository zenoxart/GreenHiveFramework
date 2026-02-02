using System.Windows;
using Zenox.Wpf.Controls.Common.CommandPalette;
using Zenox.Wpf.Core.Common.BackgroundTasks;
using Zenox.Wpf.Core.Common.Heatmap;
using Zenox.Wpf.Core.Common.Input;
using Zenox.Wpf.Core.Common.ML.ContextAwareValidation;
using Zenox.Wpf.Core.Common.MVVM.Composite;
using Zenox.Wpf.Core.Common.Notifications;
using Zenox.Wpf.Core.Common.Recovery;
using Zenox.Wpf.Core.Common.Telemetry;
using Zenox.Wpf.Core.Common.Updates;
using Zenox.Wpf.UI.Common;
using Zenox.Wpf.UI.MVVM.ViewModel;
using Zenox.Wpf.UI.Properties;

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

		// Global Keyboard Handler initialisieren
		GlobalKeyboardHandler.Initialize(() => CommandPaletteManager.Instance.ShowPalette());

		// Global Validation Manager initialisieren
		// Ermöglicht AI-basierte Validierung per Shift+Rechtsklick auf beliebigen UI-Elementen
		GlobalValidationManager.Instance.IsEnabled = true;
		GlobalValidationManager.Instance.Initialize();
		GlobalValidationManager.Instance.ElementValidated += (s, args) =>
		{
			// Optional: Logging von Validierungen
			TelemetryService.Instance.LogEvent("ElementValidated",
				$"{args.Context.FieldName}={args.Context.ValueAsString} -> {args.Result.State}");
		};

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
		ModuleHandler.AppKontext.Sprachen.Festlegen(Settings.Default.LocalisationISO);

		var Shell = new MainAppShell();
		Shell.DataContext = ModuleHandler.AppKontext;
		Shell.Show();

		ModuleHandler.LoadView<MainWindow, AppViewModel>("MainView", Shell.MainModule);

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

	protected override void OnExit(ExitEventArgs e)
	{
		Settings.Default.LocalisationISO = this.ModuleHandler.AppKontext.Sprachen.AktuelleSprache.Code;
		Settings.Default.Save();

		// Stop Heatmap Tracking
		InteractionHeatmapController.Instance.StopTracking();

		// Save final state
		CrashRecoveryManager.Instance.SaveSnapshot(false);

		_ = BackgroundTaskScheduler.Instance.StopAsync();
		GlobalKeyboardHandler.Shutdown();
		AutoUpdateService.Instance.StopAutoCheck();

		TelemetryService.Instance.LogEvent("ApplicationExit", "System");

		base.OnExit(e);
	}
}

