using System.IO;
using System.Text.Json;
using System.Windows;

namespace Zenox.Wpf.Core.Common.Recovery
{
	/// <summary>
	/// Repräsentiert den gespeicherten Anwendungs-State
	/// </summary>
	public class AppStateSnapshot
	{
		/// <summary>
		/// Zeitstempel des Snapshots
		/// </summary>
		public DateTime Timestamp { get; set; } = DateTime.Now;

		/// <summary>
		/// Gibt an, ob es sich um einen Crash-Recovery-Snapshot handelt
		/// </summary>
		public bool IsCrashRecovery { get; set; }

		/// <summary>
		/// App-Version
		/// </summary>
		public string? AppVersion { get; set; }

		/// <summary>
		/// Crash-Details (falls vorhanden)
		/// </summary>
		public string? CrashDetails { get; set; }

		/// <summary>
		/// Benutzerdefinierte State-Daten (als JSON)
		/// </summary>
		public string? CustomStateJson { get; set; }
	}

	/// <summary>
	/// Manager für Crash Recovery und App-State Management
	/// </summary>
	public sealed class CrashRecoveryManager
	{
		private static readonly Lazy<CrashRecoveryManager> _instance = new(() => new CrashRecoveryManager());

		/// <summary>
		/// Singleton-Instanz
		/// </summary>
		public static CrashRecoveryManager Instance => _instance.Value;

		private readonly string _stateFilePath;
		private AppStateSnapshot? _lastSnapshot;
		private bool _isInitialized;

		/// <summary>
		/// Delegate für Custom State Serialisierung
		/// </summary>
		public Func<object?>? GetCustomState { get; set; }

		/// <summary>
		/// Delegate für Custom State Deserialisierung
		/// </summary>
		public Action<string?>? RestoreCustomState { get; set; }

		/// <summary>
		/// Event das gefeuert wird, wenn ein Crash erkannt wurde
		/// </summary>
		public event EventHandler<AppStateSnapshot>? CrashDetected;

		private CrashRecoveryManager()
		{
			var appDataPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"VoestalpineWpf");

			Directory.CreateDirectory(appDataPath);
			_stateFilePath = Path.Combine(appDataPath, "app_state.json");
		}

		/// <summary>
		/// Initialisiert den Crash Recovery Manager
		/// </summary>
		public void Initialize()
		{
			if (_isInitialized) return;

			// Exception Handler registrieren
			Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
			AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

			// Prüfe auf vorherigen Crash
			CheckForPreviousCrash();

			// Erstelle initialen Snapshot
			SaveSnapshot(false);

			_isInitialized = true;
		}

		/// <summary>
		/// Speichert einen State-Snapshot
		/// </summary>
		public void SaveSnapshot(bool isCrashRecovery = false, string? crashDetails = null)
		{
			try
			{
				var snapshot = new AppStateSnapshot
				{
					Timestamp = DateTime.Now,
					IsCrashRecovery = isCrashRecovery,
					AppVersion = GetAppVersion(),
					CrashDetails = crashDetails
				};

				// Custom State serialisieren
				if (GetCustomState != null)
				{
					var customState = GetCustomState();
					if (customState != null)
					{
						snapshot.CustomStateJson = JsonSerializer.Serialize(customState);
					}
				}

				var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
				{
					WriteIndented = true
				});

				File.WriteAllText(_stateFilePath, json);
				_lastSnapshot = snapshot;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to save state snapshot: {ex.Message}");
			}
		}

		/// <summary>
		/// Prüft auf vorherigen Crash
		/// </summary>
		private void CheckForPreviousCrash()
		{
			try
			{
				if (!File.Exists(_stateFilePath))
					return;

				var json = File.ReadAllText(_stateFilePath);
				var snapshot = JsonSerializer.Deserialize<AppStateSnapshot>(json);

				if (snapshot?.IsCrashRecovery == true)
				{
					_lastSnapshot = snapshot;
					CrashDetected?.Invoke(this, snapshot);

					// Reset Crash-Flag
					SaveSnapshot(false);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to check for previous crash: {ex.Message}");
			}
		}

		/// <summary>
		/// Stellt den letzten State wieder her
		/// </summary>
		public void RestoreLastState()
		{
			if (_lastSnapshot?.CustomStateJson != null && RestoreCustomState != null)
			{
				RestoreCustomState(_lastSnapshot.CustomStateJson);
			}
		}

		/// <summary>
		/// Löscht den gespeicherten State
		/// </summary>
		public void ClearState()
		{
			try
			{
				if (File.Exists(_stateFilePath))
				{
					File.Delete(_stateFilePath);
				}
				_lastSnapshot = null;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to clear state: {ex.Message}");
			}
		}

		private void OnDispatcherUnhandledException(object sender,
			System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			HandleException(e.Exception);
			// Optional: e.Handled = true; um App am Laufen zu halten
		}

		private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject is Exception ex)
			{
				HandleException(ex);
			}
		}

		private void HandleException(Exception exception)
		{
			var crashDetails = $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";

			// Speichere Crash-Snapshot
			SaveSnapshot(true, crashDetails);

			// Log zu Telemetrie
			if (Telemetry.TelemetryService.Instance != null)
			{
				Telemetry.TelemetryService.Instance.LogEvent("ApplicationCrash", "Critical",
					new System.Collections.Generic.Dictionary<string, object>
					{
						["Exception"] = exception.GetType().Name,
						["Message"] = exception.Message,
						["StackTrace"] = exception.StackTrace ?? ""
					});
			}

			System.Diagnostics.Debug.WriteLine($"CRASH DETECTED: {crashDetails}");
		}

		private string GetAppVersion()
		{
			return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
		}

		/// <summary>
		/// Gibt das letzte Crash-Snapshot zurück
		/// </summary>
		public AppStateSnapshot? GetLastCrashSnapshot()
		{
			return _lastSnapshot?.IsCrashRecovery == true ? _lastSnapshot : null;
		}
	}
}
