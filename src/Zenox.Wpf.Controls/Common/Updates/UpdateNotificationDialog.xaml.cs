using System.Windows;
using Zenox.NET.Core.Common.Updates;

namespace Zenox.Wpf.Controls.Common.Updates
{
	public partial class UpdateNotificationDialog : Window
	{
		private readonly VersionInfo _versionInfo;
		private string? _downloadedFilePath;
		private bool _isDownloading;

		public UpdateNotificationDialog(VersionInfo versionInfo)
		{
			InitializeComponent();
			_versionInfo = versionInfo;

			CurrentVersionText.Text = $"Aktuelle Version: {AutoUpdateService.Instance.CurrentVersion}";
			NewVersionText.Text = $"Neue Version: {versionInfo.Version}";
			ReleaseNotesText.Text = versionInfo.ReleaseNotes;
			FileSizeText.Text = $"Größe: {FormatFileSize(versionInfo.FileSizeBytes)}";

			if (versionInfo.IsCritical)
			{
				CriticalBadge.Visibility = Visibility.Visible;
			}
		}

		private async void UpdateButton_Click(object sender, RoutedEventArgs e)
		{
			if (_isDownloading)
				return;

			_isDownloading = true;
			UpdateButton.IsEnabled = false;
			LaterButton.IsEnabled = false;
			ProgressPanel.Visibility = Visibility.Visible;

			var progress = new Progress<int>(percent =>
			{
				DownloadProgressBar.Value = percent;
				ProgressPercentText.Text = $"{percent}%";
			});

			try
			{
				ProgressText.Text = "Wird heruntergeladen...";
				_downloadedFilePath = await AutoUpdateService.Instance.DownloadUpdateAsync(_versionInfo, progress);

				if (_downloadedFilePath != null)
				{
					ProgressText.Text = "Download abgeschlossen!";
					UpdateButton.Content = "Installieren & Neustarten";
					UpdateButton.IsEnabled = true;
					UpdateButton.Click -= UpdateButton_Click;
					UpdateButton.Click += InstallButton_Click;
				}
				else
				{
					ProgressText.Text = "Download fehlgeschlagen!";
					ProgressPercentText.Foreground = System.Windows.Media.Brushes.Red;
					LaterButton.IsEnabled = true;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Fehler beim Download: {ex.Message}", "Fehler",
					MessageBoxButton.OK, MessageBoxImage.Error);
				LaterButton.IsEnabled = true;
			}
		}

		private void InstallButton_Click(object sender, RoutedEventArgs e)
		{
			if (_downloadedFilePath != null)
			{
				AutoUpdateService.Instance.InstallUpdateAndRestart(_downloadedFilePath);
			}
		}

		private void LaterButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private string FormatFileSize(long bytes)
		{
			string[] sizes = { "B", "KB", "MB", "GB" };
			double len = bytes;
			int order = 0;

			while (len >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len /= 1024;
			}

			return $"{len:0.##} {sizes[order]}";
		}
	}
}
