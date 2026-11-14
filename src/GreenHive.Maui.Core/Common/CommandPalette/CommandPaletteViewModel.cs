using System.Collections.ObjectModel;
using System.Windows.Input;
using GreenHive.NET.Core.Common.Command;
using GreenHive.NET.Core.Common.MVVM.ViewModelSupport;

namespace GreenHive.NET.Core.Common.CommandPalette
{
	/// <summary>
	/// ViewModel für die Command Palette
	/// </summary>
	public class CommandPaletteViewModel : NotificationObject
	{
		private string _searchText = string.Empty;
		private CommandPaletteItem? _selectedItem;
		private ObservableCollection<CommandPaletteItem> _allCommands;
		private ObservableCollection<CommandPaletteItem> _filteredCommands;

		public CommandPaletteViewModel()
		{
			_allCommands = new ObservableCollection<CommandPaletteItem>();
			_filteredCommands = new ObservableCollection<CommandPaletteItem>();

			ExecuteSelectedCommand = new RelayCommand(_ => ExecuteCommand(), _ => SelectedItem != null);
			CloseCommand = new RelayCommand(_ => OnCloseRequested?.Invoke());
		}

		/// <summary>
		/// Der aktuelle Suchtext
		/// </summary>
		public string SearchText
		{
			get => _searchText;
			set
			{
				if (SetProperty(ref _searchText, value))
				{
					FilterCommands();
				}
			}
		}

		/// <summary>
		/// Das aktuell ausgewählte Command
		/// </summary>
		public CommandPaletteItem? SelectedItem
		{
			get => _selectedItem;
			set
			{
				SetProperty(ref _selectedItem, value);
				// CanExecute wird automatisch durch PropertyChanged aktualisiert
			}
		}

		/// <summary>
		/// Alle verfügbaren Befehle
		/// </summary>
		public ObservableCollection<CommandPaletteItem> AllCommands
		{
			get => _allCommands;
			set => SetProperty(ref _allCommands, value);
		}

		/// <summary>
		/// Gefilterte Befehle basierend auf Suchtext
		/// </summary>
		public ObservableCollection<CommandPaletteItem> FilteredCommands
		{
			get => _filteredCommands;
			private set => SetProperty(ref _filteredCommands, value);
		}

		/// <summary>
		/// Command zum Ausführen des ausgewählten Befehls
		/// </summary>
		public ICommand ExecuteSelectedCommand { get; }

		/// <summary>
		/// Command zum Schließen der Palette
		/// </summary>
		public ICommand CloseCommand { get; }

		/// <summary>
		/// Event, das ausgelöst wird, wenn die Palette geschlossen werden soll
		/// </summary>
		public Action? OnCloseRequested { get; set; }

		/// <summary>
		/// Fügt einen Befehl zur Palette hinzu
		/// </summary>
		public void AddCommand(CommandPaletteItem item)
		{
			AllCommands.Add(item);
			FilterCommands();
		}

		/// <summary>
		/// Fügt mehrere Befehle zur Palette hinzu
		/// </summary>
		public void AddCommands(IEnumerable<CommandPaletteItem> items)
		{
			foreach (var item in items)
			{
				AllCommands.Add(item);
			}
			FilterCommands();
		}

		/// <summary>
		/// Entfernt alle Befehle
		/// </summary>
		public void ClearCommands()
		{
			AllCommands.Clear();
			FilteredCommands.Clear();
		}

		/// <summary>
		/// Filtert die Befehle basierend auf dem Suchtext
		/// </summary>
		private void FilterCommands()
		{
			if (string.IsNullOrWhiteSpace(SearchText))
			{
				FilteredCommands = new ObservableCollection<CommandPaletteItem>(AllCommands);
			}
			else
			{
				var matches = AllCommands
					.Select(cmd => new { Command = cmd, Score = cmd.CalculateMatchScore(SearchText) })
					.Where(x => x.Score > 0)
					.OrderByDescending(x => x.Score)
					.Select(x => x.Command)
					.ToList();

				FilteredCommands = new ObservableCollection<CommandPaletteItem>(matches);
			}

			// Erstes Element automatisch auswählen
			if (FilteredCommands.Any())
			{
				SelectedItem = FilteredCommands.First();
			}
			else
			{
				SelectedItem = null;
			}
		}

		/// <summary>
		/// Führt den ausgewählten Befehl aus
		/// </summary>
		private void ExecuteCommand()
		{
			if (SelectedItem?.Command != null && SelectedItem.Command.CanExecute(SelectedItem.CommandParameter))
			{
				SelectedItem.Command.Execute(SelectedItem.CommandParameter);
				OnCloseRequested?.Invoke();
			}
		}

		/// <summary>
		/// Wählt das nächste Element in der Liste aus
		/// </summary>
		public void SelectNext()
		{
			if (!FilteredCommands.Any() || SelectedItem == null)
				return;

			var currentIndex = FilteredCommands.IndexOf(SelectedItem);
			if (currentIndex < FilteredCommands.Count - 1)
			{
				SelectedItem = FilteredCommands[currentIndex + 1];
			}
		}

		/// <summary>
		/// Wählt das vorherige Element in der Liste aus
		/// </summary>
		public void SelectPrevious()
		{
			if (!FilteredCommands.Any() || SelectedItem == null)
				return;

			var currentIndex = FilteredCommands.IndexOf(SelectedItem);
			if (currentIndex > 0)
			{
				SelectedItem = FilteredCommands[currentIndex - 1];
			}
		}
	}
}
