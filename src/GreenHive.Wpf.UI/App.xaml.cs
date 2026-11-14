using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using GreenHive.NET.Core.Common.BackgroundTasks;
using GreenHive.NET.Core.Common.Heatmap;
using GreenHive.NET.Core.Common.Notifications;
using GreenHive.NET.Core.Common.Recovery;
using GreenHive.NET.Core.Common.Telemetry;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Workflows;
using GreenHive.Wpf.UI.Platform;
using GreenHive.Wpf.UI.Platform.Shell;

namespace GreenHive.Wpf.UI;

/// <summary>
/// Green Hive host application. Creates the client application context (composition root), registers texts, icons,
/// shell commands, built-in, workflow and classic views, opens the shell window and then runs the startup sequence
/// (sign-in, modules from the server, dock layout, live notifications, hot-swap). The classic Green Hive services
/// (telemetry, interaction heatmap, crash recovery, background tasks) keep running alongside the platform.
/// </summary>
public partial class App : Application
{
	private ShellWindow? _shell;
	private StartupSequence? _startup;

	/// <summary>The root application context (WIFI: AppKontext of the client).</summary>
	public static ClientAppContext Context { get; } = new();

	private void Application_Startup(object sender, StartupEventArgs e)
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;

		// ---- classic Green Hive services
		GreenHive.Wpf.Views.Helpers.WindowManagerServiceLocator.Instance = new Services.WindowManagerService();
		GreenHive.Wpf.Views.Helpers.LanguageServiceLocator.Instance = new Services.LanguageService();
		InitializeClassicLocalization();
		BackgroundTaskScheduler.Instance.Start();
		TelemetryService.Instance.Initialize();
		InteractionHeatmapController.Instance.Initialize(new HeatmapConfiguration
		{
			TrackClicks = true,
			TrackHover = true,
			TrackScroll = false,
			MinHoverDurationMs = 500,
			MaxInteractions = 10000,
		});
		InteractionHeatmapController.Instance.StartTracking();
		OfferCrashRecovery();

		// ---- the platform (composition root)
		var context = Context;
		context.Dispatcher = context.Commands.Dispatcher;
		context.Commands.Dispatcher.Register("app", new WpfDispatcherQueue(Dispatcher), isDefault: true);
		HostTexts.Register(context.Languages);
		PlatformTexts.Register(context.Languages);
		WorkflowTexts.Register(context.Languages);
		GreenHiveTexts.Register(context.Languages);
		context.Languages.Set(context.Settings.Current.Language ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
		context.Log.Write("Green Hive starts", LogEntryType.Start, nameof(App));
		RegisterIcons(context);
		Ui.Initialize(context.Commands);

		// UI gating only - every service authorizes again on the server.
		context.Commands.Executor.PermissionCheck = permission => context.Backend.Has(permission);
		ShellCommands.Register(context, () => _shell, () => _startup);
		BuiltInViews.Register(context);

		_shell = new ShellWindow(context);
		MainWindow = _shell;
		_startup = new StartupSequence(context, _shell);
		_startup.Status += (_, text) => _shell.ViewModel.SetStartupStatus(text);
		_shell.AttachProfile(() => _startup.SignInAsync());

		// Platform notifications also appear as classic in-window toasts while the app is in the foreground.
		context.InApp.Added += (_, item) => Dispatcher.BeginInvoke(() =>
		{
			if (_shell?.IsShellActive != true)
			{
				return;
			}

			var message = item.Message ?? string.Empty;
			Action? open = item.NavigationTarget is { } target ? () => context.Navigation.NavigateTo(target, item.NavigationParameter) : null;
			switch (item.Severity)
			{
				case NotificationSeverity.Success:
					NotificationService.Instance.ShowSuccess(item.Title, message, 5, open);
					break;
				case NotificationSeverity.Warning:
					NotificationService.Instance.ShowWarning(item.Title, message, 6, open);
					break;
				case NotificationSeverity.Error:
					NotificationService.Instance.ShowError(item.Title, message, 8, open);
					break;
				default:
					NotificationService.Instance.ShowInfo(item.Title, message, 5, open);
					break;
			}
		});

		_shell.Show();
		_shell.WhenLoaded(async () =>
		{
			try
			{
				await _startup.RunAsync();
			}
			catch (Exception ex)
			{
				context.Log.Write($"Startup failed: {ex.Message}", LogEntryType.Error, nameof(App), ex);
			}
		});

		TelemetryService.Instance.LogEvent("ApplicationStarted", "System");
		BackgroundTaskScheduler.Instance.ScheduleRecurringTask(
			"Auto-Save State",
			async ct => await Task.Run(() => CrashRecoveryManager.Instance.SaveSnapshot(false), ct),
			TimeSpan.FromMinutes(5),
			TaskPriority.Low);
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try
		{
			Context.Settings.Save();
			InteractionHeatmapController.Instance.StopTracking();
			CrashRecoveryManager.Instance.SaveSnapshot(false);
			_ = BackgroundTaskScheduler.Instance.StopAsync();
			TelemetryService.Instance.LogEvent("ApplicationExit", "System");
		}
		catch (Exception ex)
		{
			Context.Log.Write($"Shutdown: {ex.Message}", LogEntryType.Warning, nameof(App), ex);
		}

		base.OnExit(e);
	}

	private static void RegisterIcons(ClientAppContext context)
	{
		var icons = context.Icons;
		icons.RegisterGlyph("notifications", "\uEA8F");
		icons.RegisterGlyph("signin", "\uE77B");
		icons.RegisterGlyph("signout", "\uF3B1");
		icons.RegisterGlyph("sync", "\uE895");
		icons.RegisterGlyph("attach", "\uE90C");
		icons.RegisterGlyph("workflow", "\uE9D5");
		icons.RegisterGlyph("workflow.inbox", "\uE715");
		icons.RegisterGlyph("workflow.impact", "\uE7BA");
		icons.RegisterGlyph("workflow.ai", "\uE99A");
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		Context.Log.Write($"Unhandled exception: {e.Exception.Message}", LogEntryType.Error, nameof(App), e.Exception);
		TelemetryService.Instance.LogEvent("UnhandledException", "System");
		e.Handled = true;
	}

	/// <summary>Offers to restore the last session after a crash (classic Green Hive crash recovery).</summary>
	private static void OfferCrashRecovery()
	{
		var crashSnapshot = CrashRecoveryManager.Instance.GetLastCrashSnapshot();
		if (crashSnapshot is null)
		{
			return;
		}

		var crashDialog = new Controls.Common.Recovery.CrashRecoveryDialog(crashSnapshot);
		if (crashDialog.ShowDialog() == true && crashDialog.ShouldRestore)
		{
			CrashRecoveryManager.Instance.RestoreLastState();
			NotificationService.Instance.ShowSuccess("Wiederherstellung erfolgreich", "Die letzte Sitzung wurde wiederhergestellt");
		}
	}

	/// <summary>Initializes the localization of the classic views (resource dictionaries de-DE / en-US).</summary>
	private static void InitializeClassicLocalization()
	{
		try
		{
			Common.Localization.LocalizationManager.Instance.Initialize();
#if DEBUG
			Common.Localization.LocalizationDiagnostics.RunDiagnostics();
#endif
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Classic localization failed: {ex.Message}");
		}
	}
}
