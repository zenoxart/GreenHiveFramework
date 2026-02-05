using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Zenox.NET.Core.Common.CommandPalette;

namespace Zenox.Wpf.Controls.Common.CommandPalette
{
	/// <summary>
	/// Command Palette Window - Schnellzugriff auf alle Befehle
	/// </summary>
	public partial class CommandPaletteWindow : System.Windows.Window
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
}
