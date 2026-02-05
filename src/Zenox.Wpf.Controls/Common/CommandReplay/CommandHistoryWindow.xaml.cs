using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Zenox.Wpf.Controls.Common.Dialogs;
using Zenox.NET.Core.Common.CommandReplay;
using Zenox.NET.Core.Common.Notifications;

namespace Zenox.Wpf.Controls.Common.CommandReplay
{
	public partial class CommandHistoryWindow : Window
	{
		public CommandHistoryWindow()
		{
			InitializeComponent();
			LoadHistory();

			RecordingCheckBox.IsChecked = CommandReplayManager.Instance.IsRecording;

			CommandReplayManager.Instance.CommandRecorded += (s, cmd) =>
			{
				Dispatcher.Invoke(LoadHistory);
			};
		}

		private void LoadHistory()
		{
			var history = CommandReplayManager.Instance.GetHistory(500);
			CommandHistoryGrid.ItemsSource = history;
			CommandCountText.Text = history.Count.ToString();
		}

		private void RecordingCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			CommandReplayManager.Instance.IsRecording = RecordingCheckBox.IsChecked == true;
		}

		private void CommandHistoryGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			SelectedCountText.Text = CommandHistoryGrid.SelectedItems.Count.ToString();
		}

		private async void ReplaySelected_Click(object sender, RoutedEventArgs e)
		{
			var selected = CommandHistoryGrid.SelectedItems.Cast<RecordedCommand>().ToList();

			if (!selected.Any())
			{
				MessageBox.Show("Bitte wählen Sie mindestens einen Befehl aus.", "Keine Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var result = MessageBox.Show(
				$"Möchten Sie {selected.Count} Befehl(e) wiedergeben?",
				"Befehle wiedergeben",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);

			if (result == MessageBoxResult.Yes)
			{
				NotificationService.Instance.ShowInfo(
					"Wiedergabe gestartet",
					$"{selected.Count} Befehle werden wiedergegeben...");

				var replayResult = await CommandReplayManager.Instance.ReplaySequence(selected);

				if (replayResult.AllSuccessful)
				{
					NotificationService.Instance.ShowSuccess(
						"Wiedergabe erfolgreich",
						$"Alle {replayResult.TotalCommands} Befehle wurden erfolgreich ausgeführt");
				}
				else
				{
					NotificationService.Instance.ShowWarning(
						"Wiedergabe abgeschlossen",
						$"{replayResult.SuccessfulCommands} erfolgreich, {replayResult.FailedCommands} fehlgeschlagen");
				}
			}
		}

		private void SaveSequence_Click(object sender, RoutedEventArgs e)
		{
			var selected = CommandHistoryGrid.SelectedItems.Cast<RecordedCommand>().ToList();

			if (!selected.Any())
			{
				MessageBox.Show("Bitte wählen Sie mindestens einen Befehl aus.", "Keine Auswahl", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var inputDialog = new InputDialog("Sequenz speichern", "Bitte geben Sie einen Namen für die Sequenz ein:");
			if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
			{
				CommandReplayManager.Instance.SaveSequence(selected, inputDialog.InputText);
				NotificationService.Instance.ShowSuccess(
					"Sequenz gespeichert",
					$"Sequenz '{inputDialog.InputText}' mit {selected.Count} Befehlen gespeichert");
			}
		}

		private async void LoadSequence_Click(object sender, RoutedEventArgs e)
		{
			var sequences = CommandReplayManager.Instance.GetSavedSequences();

			if (!sequences.Any())
			{
				MessageBox.Show("Keine gespeicherten Sequenzen gefunden.", "Keine Sequenzen", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Simple selection dialog
			var dialog = new System.Windows.Controls.ListBox
			{
				ItemsSource = sequences
			};

			var window = new Window
			{
				Title = "Sequenz auswählen",
				Width = 400,
				Height = 300,
				WindowStartupLocation = WindowStartupLocation.CenterScreen,
				Content = dialog,
				Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"))
			};

			if (window.ShowDialog() == true && dialog.SelectedItem is string selectedName)
			{
				var sequence = CommandReplayManager.Instance.LoadSequence(selectedName);
				if (sequence != null)
				{
					var result = MessageBox.Show(
						$"Möchten Sie die Sequenz '{sequence.Name}' mit {sequence.Commands.Count} Befehlen wiedergeben?",
						"Sequenz wiedergeben",
						MessageBoxButton.YesNo,
						MessageBoxImage.Question);

					if (result == MessageBoxResult.Yes)
					{
						NotificationService.Instance.ShowInfo(
							"Sequenz wird wiedergegeben",
							$"'{sequence.Name}' mit {sequence.Commands.Count} Befehlen...");

						var replayResult = await CommandReplayManager.Instance.ReplaySequence(sequence.Commands);

						if (replayResult.AllSuccessful)
						{
							NotificationService.Instance.ShowSuccess(
								"Wiedergabe erfolgreich",
								$"Alle {replayResult.TotalCommands} Befehle wurden erfolgreich ausgeführt");
						}
						else
						{
							NotificationService.Instance.ShowWarning(
								"Wiedergabe abgeschlossen",
								$"{replayResult.SuccessfulCommands} erfolgreich, {replayResult.FailedCommands} fehlgeschlagen");
						}
					}
				}
			}
		}

		private void ClearHistory_Click(object sender, RoutedEventArgs e)
		{
			var result = MessageBox.Show(
				"Möchten Sie den gesamten Befehlsverlauf löschen?",
				"Verlauf löschen",
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning);

			if (result == MessageBoxResult.Yes)
			{
				CommandReplayManager.Instance.ClearHistory();
				LoadHistory();
				NotificationService.Instance.ShowInfo("Verlauf gelöscht", "Der Befehlsverlauf wurde gelöscht");
			}
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}

	public class BoolToStatusConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (bool)value ? "?" : "?";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class BoolToColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (bool)value ?
				new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#107C10")) :
				new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E81123"));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
