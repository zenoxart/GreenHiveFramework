using System.Windows;
using Zenox.NET.Core.Common.Recovery;

namespace Zenox.Wpf.Controls.Common.Recovery
{
	public partial class CrashRecoveryDialog : Window
	{
		private bool _shouldRestore;
		private readonly AppStateSnapshot _crashSnapshot;

		public bool ShouldRestore => _shouldRestore;

		public CrashRecoveryDialog(AppStateSnapshot crashSnapshot)
		{
			InitializeComponent();
			_crashSnapshot = crashSnapshot;

			TimestampText.Text = $"Zeitpunkt: {crashSnapshot.Timestamp:dd.MM.yyyy HH:mm:ss}";

			if (!string.IsNullOrEmpty(crashSnapshot.CrashDetails))
			{
				CrashDetailsText.Text = crashSnapshot.CrashDetails;
			}
		}

		private void ToggleDetailsButton_Click(object sender, RoutedEventArgs e)
		{
			if (DetailsPanel.Visibility == Visibility.Collapsed)
			{
				DetailsPanel.Visibility = Visibility.Visible;
				ToggleDetailsButton.Content = "Details ausblenden";
			}
			else
			{
				DetailsPanel.Visibility = Visibility.Collapsed;
				ToggleDetailsButton.Content = "Details anzeigen";
			}
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			_shouldRestore = true;
			DialogResult = true;
			Close();
		}

		private void RestartButton_Click(object sender, RoutedEventArgs e)
		{
			_shouldRestore = false;
			DialogResult = false;
			Close();
		}
	}
}
