namespace Zenox.NET.Core.Common.Updates
{
	/// <summary>
	/// Repräsentiert Versions-Informationen
	/// </summary>
	public class VersionInfo
	{
		/// <summary>
		/// Version
		/// </summary>
		public Version Version { get; set; } = new Version(1, 0, 0);

		/// <summary>
		/// Release-Notes
		/// </summary>
		public string ReleaseNotes { get; set; } = string.Empty;

		/// <summary>
		/// Download-URL für das Update
		/// </summary>
		public string DownloadUrl { get; set; } = string.Empty;

		/// <summary>
		/// Veröffentlichungsdatum
		/// </summary>
		public DateTime ReleaseDate { get; set; } = DateTime.Now;

		/// <summary>
		/// Ist dieses Update kritisch?
		/// </summary>
		public bool IsCritical { get; set; }

		/// <summary>
		/// Dateigröße in Bytes
		/// </summary>
		public long FileSizeBytes { get; set; }
	}

	/// <summary>
	/// Update-Status
	/// </summary>
	public enum UpdateStatus
	{
		/// <summary>
		/// Keine Updates verfügbar
		/// </summary>
		NoUpdateAvailable,

		/// <summary>
		/// Update verfügbar
		/// </summary>
		UpdateAvailable,

		/// <summary>
		/// Download läuft
		/// </summary>
		Downloading,

		/// <summary>
		/// Download abgeschlossen
		/// </summary>
		DownloadComplete,

		/// <summary>
		/// Installation läuft
		/// </summary>
		Installing,

		/// <summary>
		/// Fehler aufgetreten
		/// </summary>
		Error
	}

	/// <summary>
	/// Update-Event-Argumente
	/// </summary>
	public class UpdateEventArgs : EventArgs
	{
		public UpdateStatus Status { get; set; }
		public VersionInfo? VersionInfo { get; set; }
		public int ProgressPercentage { get; set; }
		public string? ErrorMessage { get; set; }
	}
}
