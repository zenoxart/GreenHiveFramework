using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Zenox.Wpf.Core.Common.Snapshots;

namespace Zenox.Wpf.Controls.Common.Snapshots
{
	public partial class SnapshotManagerWindow : Window
	{
		public StateSnapshot SelectedSnapshot { get; private set; }
		public bool ShouldRestore { get; private set; }

		public SnapshotManagerWindow()
		{
			InitializeComponent();
			LoadSnapshots();

			SnapshotManager.Instance.SnapshotCreated += (s, snapshot) =>
			{
				Dispatcher.Invoke(LoadSnapshots);
			};
		}

		private void LoadSnapshots()
		{
			var snapshots = SnapshotManager.Instance.GetSnapshots();
			SnapshotList.ItemsSource = snapshots;

			TotalCountText.Text = snapshots.Count.ToString();
			var totalBytes = SnapshotManager.Instance.GetTotalSize();
			TotalSizeText.Text = $"{totalBytes / 1024.0 / 1024.0:F2} MB";
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is System.Windows.Controls.Button button && button.Tag is StateSnapshot snapshot)
			{
				var result = MessageBox.Show(
					$"Möchten Sie den Snapshot '{snapshot.Name}' wiederherstellen?\n\nErstellt am: {snapshot.Timestamp:dd.MM.yyyy HH:mm:ss}",
					"Snapshot wiederherstellen",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (result == MessageBoxResult.Yes)
				{
					SelectedSnapshot = snapshot;
					ShouldRestore = true;
					DialogResult = true;
					Close();
				}
			}
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is System.Windows.Controls.Button button && button.Tag is StateSnapshot snapshot)
			{
				var result = MessageBox.Show(
					$"Möchten Sie den Snapshot '{snapshot.Name}' wirklich löschen?",
					"Snapshot löschen",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning);

				if (result == MessageBoxResult.Yes)
				{
					SnapshotManager.Instance.DeleteSnapshot(snapshot.Id);
					LoadSnapshots();
				}
			}
		}

		private void CreateSnapshot_Click(object sender, RoutedEventArgs e)
		{
			var inputDialog = new InputDialog("Neuer Snapshot", "Bitte geben Sie einen Namen ein:");
			if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
			{
				// Create snapshot with app state
				// Note: In real usage, you would capture actual app state here
				var dummyState = new { Message = "Snapshot erstellt", Timestamp = DateTime.Now };
				SnapshotManager.Instance.CreateSnapshot(dummyState, inputDialog.InputText, SnapshotType.Manual);
				LoadSnapshots();
			}
		}

		private void ClearAll_Click(object sender, RoutedEventArgs e)
		{
			var result = MessageBox.Show(
				"Möchten Sie wirklich ALLE Snapshots löschen?\n\nDieser Vorgang kann nicht rückgängig gemacht werden!",
				"Alle Snapshots löschen",
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning);

			if (result == MessageBoxResult.Yes)
			{
				SnapshotManager.Instance.ClearAll();
				LoadSnapshots();
			}
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}

	// Simple input dialog for snapshot name
	public class InputDialog : Window
	{
		private System.Windows.Controls.TextBox _textBox;

		public string InputText => _textBox.Text;

		public InputDialog(string title, string prompt)
		{
			Title = title;
			Width = 400;
			Height = 180;
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"));

			var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
			grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
			grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
			grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
			grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

			var promptText = new System.Windows.Controls.TextBlock
			{
				Text = prompt,
				Foreground = System.Windows.Media.Brushes.White,
				FontSize = 14,
				Margin = new Thickness(0, 0, 0, 10)
			};
			System.Windows.Controls.Grid.SetRow(promptText, 0);

			_textBox = new System.Windows.Controls.TextBox
			{
				Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D2D")),
				Foreground = System.Windows.Media.Brushes.White,
				Padding = new Thickness(8),
				FontSize = 14,
				BorderThickness = new Thickness(1),
				BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A3A3A"))
			};
			System.Windows.Controls.Grid.SetRow(_textBox, 1);

			var buttonPanel = new System.Windows.Controls.StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(0, 15, 0, 0)
			};
			System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

			var okButton = new System.Windows.Controls.Button
			{
				Content = "OK",
				Width = 80,
				Margin = new Thickness(0, 0, 10, 0),
				Padding = new Thickness(0, 8, 0, 8),
				Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.MediumPurple),
				Foreground = System.Windows.Media.Brushes.White,
				BorderThickness = new Thickness(0),
				Cursor = System.Windows.Input.Cursors.Hand
			};
			okButton.Click += (s, e) => { DialogResult = true; Close(); };

			var cancelButton = new System.Windows.Controls.Button
			{
				Content = "Abbrechen",
				Width = 80,
				Padding = new Thickness(0, 8, 0, 8),
				Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D2D")),
				Foreground = System.Windows.Media.Brushes.White,
				BorderThickness = new Thickness(0),
				Cursor = System.Windows.Input.Cursors.Hand
			};
			cancelButton.Click += (s, e) => Close();

			buttonPanel.Children.Add(okButton);
			buttonPanel.Children.Add(cancelButton);

			grid.Children.Add(promptText);
			grid.Children.Add(_textBox);
			grid.Children.Add(buttonPanel);

			Content = grid;
		}
	}

	// Converter for bytes to KB
	public class BytesToKbConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is long bytes)
				return bytes / 1024.0;
			return 0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
