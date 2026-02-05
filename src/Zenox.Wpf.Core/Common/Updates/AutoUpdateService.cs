using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Zenox.NET.Core.Common.Updates
{
	/// <summary>
	/// Service für automatische Updates
	/// </summary>
	public sealed class AutoUpdateService
	{
		private static readonly Lazy<AutoUpdateService> _instance = new(() => new AutoUpdateService());

		/// <summary>
		/// Singleton-Instanz
		/// </summary>
		public static AutoUpdateService Instance => _instance.Value;

		private readonly HttpClient _httpClient;
		private VersionInfo? _latestVersion;
		private string _updateDownloadPath;

		/// <summary>
		/// URL zum Update-Manifest (JSON mit VersionInfo)
		/// </summary>
		public string UpdateManifestUrl { get; set; } = string.Empty;

		/// <summary>
		/// Aktuelle App-Version
		/// </summary>
		public Version CurrentVersion { get; private set; }

		/// <summary>
		/// Automatische Update-Prüfung beim Start aktiviert?
		/// </summary>
		public bool AutoCheckOnStartup { get; set; } = true;

		/// <summary>
		/// Intervall für automatische Update-Prüfung (0 = deaktiviert)
		/// </summary>
		public TimeSpan AutoCheckInterval { get; set; } = TimeSpan.FromHours(6);

		/// <summary>
		/// Event das gefeuert wird, wenn Update-Status sich ändert
		/// </summary>
		public event EventHandler<UpdateEventArgs>? UpdateStatusChanged;

		/// <summary>
		/// Event das gefeuert wird, wenn ein neues Update verfügbar ist
		/// </summary>
		public event EventHandler<VersionInfo>? UpdateAvailable;

		private CancellationTokenSource? _autoCheckCancellation;

		private AutoUpdateService()
		{
			_httpClient = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(30)
			};

			CurrentVersion = GetCurrentAppVersion();

			var appDataPath = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"VoestalpineWpf", "Updates");

			Directory.CreateDirectory(appDataPath);
			_updateDownloadPath = appDataPath;
		}

		/// <summary>
		/// Initialisiert den Update-Service
		/// </summary>
		public void Initialize(string updateManifestUrl)
		{
			UpdateManifestUrl = updateManifestUrl;

			if (AutoCheckOnStartup)
			{
				_ = CheckForUpdatesAsync();
			}

			if (AutoCheckInterval > TimeSpan.Zero)
			{
				StartAutoCheck();
			}
		}

		/// <summary>
		/// Prüft auf verfügbare Updates
		/// </summary>
		public async Task<VersionInfo?> CheckForUpdatesAsync()
		{
			try
			{
				if (string.IsNullOrEmpty(UpdateManifestUrl))
				{
					RaiseStatusChanged(UpdateStatus.Error, null, 0, "Update-Manifest-URL nicht konfiguriert");
					return null;
				}

				var response = await _httpClient.GetStringAsync(UpdateManifestUrl);
				var versionInfo = JsonSerializer.Deserialize<VersionInfo>(response);

				if (versionInfo == null)
				{
					RaiseStatusChanged(UpdateStatus.Error, null, 0, "Ungültiges Update-Manifest");
					return null;
				}

				_latestVersion = versionInfo;

				if (versionInfo.Version > CurrentVersion)
				{
					RaiseStatusChanged(UpdateStatus.UpdateAvailable, versionInfo);
					UpdateAvailable?.Invoke(this, versionInfo);
					return versionInfo;
				}
				else
				{
					RaiseStatusChanged(UpdateStatus.NoUpdateAvailable, versionInfo);
					return null;
				}
			}
			catch (Exception ex)
			{
				RaiseStatusChanged(UpdateStatus.Error, null, 0, $"Update-Prüfung fehlgeschlagen: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Lädt das Update herunter
		/// </summary>
		public async Task<string?> DownloadUpdateAsync(VersionInfo versionInfo,
			IProgress<int>? progress = null, CancellationToken cancellationToken = default)
		{
			try
			{
				RaiseStatusChanged(UpdateStatus.Downloading, versionInfo, 0);

				var fileName = $"update_{versionInfo.Version}.exe";
				var filePath = Path.Combine(_updateDownloadPath, fileName);

				using var response = await _httpClient.GetAsync(versionInfo.DownloadUrl,
					HttpCompletionOption.ResponseHeadersRead, cancellationToken);

				response.EnsureSuccessStatusCode();

				var totalBytes = response.Content.Headers.ContentLength ?? 0;
				var buffer = new byte[8192];
				var totalRead = 0L;

				await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
				await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write,
					FileShare.None, 8192, true);

				int bytesRead;
				while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
				{
					await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
					totalRead += bytesRead;

					if (totalBytes > 0)
					{
						var progressPercent = (int)((totalRead * 100) / totalBytes);
						progress?.Report(progressPercent);
						RaiseStatusChanged(UpdateStatus.Downloading, versionInfo, progressPercent);
					}
				}

				RaiseStatusChanged(UpdateStatus.DownloadComplete, versionInfo, 100);
				return filePath;
			}
			catch (Exception ex)
			{
				RaiseStatusChanged(UpdateStatus.Error, versionInfo, 0,
					$"Download fehlgeschlagen: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Installiert das Update und startet die App neu
		/// </summary>
		public void InstallUpdateAndRestart(string installerPath)
		{
			try
			{
				RaiseStatusChanged(UpdateStatus.Installing, _latestVersion);

				// Starte Installer
				var startInfo = new ProcessStartInfo
				{
					FileName = installerPath,
					UseShellExecute = true,
					Verb = "runas" // Als Administrator ausführen
				};

				Process.Start(startInfo);

				// Beende aktuelle App
				System.Windows.Application.Current.Shutdown();
			}
			catch (Exception ex)
			{
				RaiseStatusChanged(UpdateStatus.Error, _latestVersion, 0,
					$"Installation fehlgeschlagen: {ex.Message}");
			}
		}

		/// <summary>
		/// Startet automatische Update-Prüfung
		/// </summary>
		private void StartAutoCheck()
		{
			_autoCheckCancellation = new CancellationTokenSource();

			Task.Run(async () =>
			{
				while (!_autoCheckCancellation.Token.IsCancellationRequested)
				{
					try
					{
						await Task.Delay(AutoCheckInterval, _autoCheckCancellation.Token);
						await CheckForUpdatesAsync();
					}
					catch (TaskCanceledException)
					{
						break;
					}
				}
			}, _autoCheckCancellation.Token);
		}

		/// <summary>
		/// Stoppt automatische Update-Prüfung
		/// </summary>
		public void StopAutoCheck()
		{
			_autoCheckCancellation?.Cancel();
		}

		private void RaiseStatusChanged(UpdateStatus status, VersionInfo? versionInfo = null,
			int progress = 0, string? errorMessage = null)
		{
			UpdateStatusChanged?.Invoke(this, new UpdateEventArgs
			{
				Status = status,
				VersionInfo = versionInfo,
				ProgressPercentage = progress,
				ErrorMessage = errorMessage
			});

			// Log zu Telemetrie
			if (Telemetry.TelemetryService.Instance != null)
			{
				Telemetry.TelemetryService.Instance.LogEvent($"Update_{status}", "System",
					new System.Collections.Generic.Dictionary<string, object>
					{
						["Version"] = versionInfo?.Version.ToString() ?? "N/A",
						["Progress"] = progress,
						["Error"] = errorMessage ?? ""
					});
			}
		}

		private Version GetCurrentAppVersion()
		{
			return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
		}

		/// <summary>
		/// Gibt die neueste verfügbare Version zurück
		/// </summary>
		public VersionInfo? GetLatestVersion() => _latestVersion;
	}
}
