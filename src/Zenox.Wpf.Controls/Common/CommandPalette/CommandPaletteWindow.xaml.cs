using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Zenox.Wpf.Core.Common.CommandPalette;

namespace Zenox.Wpf.Controls.Common.CommandPalette
{
	/// <summary>
	/// Command Palette Window - Schnellzugriff auf alle Befehle
	/// </summary>
	public partial class CommandPaletteWindow : Window
	{
		private CommandPaletteViewModel? _viewModel;

		public CommandPaletteWindow()
		{
			InitializeComponent();
			Loaded += CommandPaletteWindow_Loaded;
		}

		public CommandPaletteViewModel ViewModel
		{
			get => _viewModel ??= new CommandPaletteViewModel();
			set
			{
				_viewModel = value;
				DataContext = _viewModel;
				if (_viewModel != null)
				{
					_viewModel.OnCloseRequested = Close;
				}
			}
		}

		private void CommandPaletteWindow_Loaded(object sender, RoutedEventArgs e)
		{
			// Fokus auf Suchfeld setzen
			SearchBox.Focus();

			// ViewModel initialisieren wenn noch nicht gesetzt
			if (DataContext == null)
			{
				ViewModel = new CommandPaletteViewModel();
			}
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (_viewModel == null)
				return;

			switch (e.Key)
			{
				case Key.Escape:
					Close();
					e.Handled = true;
					break;

				case Key.Enter:
					_viewModel.ExecuteSelectedCommand.Execute(null);
					e.Handled = true;
					break;

				case Key.Down:
					_viewModel.SelectNext();
					CommandList.ScrollIntoView(_viewModel.SelectedItem);
					e.Handled = true;
					break;

				case Key.Up:
					_viewModel.SelectPrevious();
					CommandList.ScrollIntoView(_viewModel.SelectedItem);
					e.Handled = true;
					break;
			}
		}

		private void CommandList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (_viewModel?.SelectedItem != null)
			{
				_viewModel.ExecuteSelectedCommand.Execute(null);
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}

	/// <summary>
	/// Converter für Textlänge zu Visibility (für Placeholder)
	/// </summary>
	public class TextLengthToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int length)
			{
				return length == 0 ? Visibility.Visible : Visibility.Collapsed;
			}
			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Converter für Null zu Visibility
	/// </summary>
	public class NullToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null || (value is string str && string.IsNullOrEmpty(str))
				? Visibility.Collapsed
				: Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
