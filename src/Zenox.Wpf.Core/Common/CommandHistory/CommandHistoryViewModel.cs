using System.Collections.ObjectModel;
using System.Windows.Input;
using Zenox.Wpf.Core.Common.Command;
using Zenox.Wpf.Core.Common.CommandReplay;
using Zenox.Wpf.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.Core.Common.CommandHistory
{
    /// <summary>
    /// ViewModel fuer das Command History Overlay
    /// </summary>
    public class CommandHistoryViewModel : NotificationObject
    {
        private string _searchText = string.Empty;
        private RecordedCommand? _selectedItem;
        private ObservableCollection<RecordedCommand> _allCommands;
        private ObservableCollection<RecordedCommand> _filteredCommands;

        public CommandHistoryViewModel()
        {
            _allCommands = new ObservableCollection<RecordedCommand>();
            _filteredCommands = new ObservableCollection<RecordedCommand>();

            ReplaySelectedCommand = new RelayCommand(_ => ReplayCommand(), _ => SelectedItem != null)
            { Name = "ReplaySelectedCommand", TrackExecution = false };
            
            ClearHistoryCommand = new RelayCommand(_ => ClearHistory(), _ => AllCommands.Count > 0)
            { Name = "ClearHistoryCommand", TrackExecution = false };
            
            CloseCommand = new RelayCommand(_ => OnCloseRequested?.Invoke())
            { Name = "CloseHistoryCommand", TrackExecution = false };

            // Lade initiale Historie
            RefreshHistory();

            // Abonniere Events fuer Live-Updates
            CommandReplayManager.Instance.CommandRecorded += (s, cmd) =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _allCommands.Insert(0, cmd);
                    FilterCommands();
                });
            };
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
        /// Das aktuell ausgewaehlte Command
        /// </summary>
        public RecordedCommand? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        /// <summary>
        /// Alle Commands
        /// </summary>
        public ObservableCollection<RecordedCommand> AllCommands
        {
            get => _allCommands;
            set => SetProperty(ref _allCommands, value);
        }

        /// <summary>
        /// Gefilterte Commands basierend auf Suchtext
        /// </summary>
        public ObservableCollection<RecordedCommand> FilteredCommands
        {
            get => _filteredCommands;
            private set => SetProperty(ref _filteredCommands, value);
        }

        /// <summary>
        /// Gibt an, ob die Aufzeichnung aktiv ist
        /// </summary>
        public bool IsRecording
        {
            get => CommandReplayManager.Instance.IsRecording;
            set
            {
                CommandReplayManager.Instance.IsRecording = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Command zum Wiedergeben des ausgewaehlten Befehls
        /// </summary>
        public ICommand ReplaySelectedCommand { get; }

        /// <summary>
        /// Command zum Loeschen der Historie
        /// </summary>
        public ICommand ClearHistoryCommand { get; }

        /// <summary>
        /// Command zum Schliessen
        /// </summary>
        public ICommand CloseCommand { get; }

        /// <summary>
        /// Event, das ausgeloest wird, wenn geschlossen werden soll
        /// </summary>
        public Action? OnCloseRequested { get; set; }

        /// <summary>
        /// Aktualisiert die Historie
        /// </summary>
        public void RefreshHistory()
        {
            _allCommands.Clear();
            var history = CommandReplayManager.Instance.GetHistory(500);
            foreach (var cmd in history)
            {
                _allCommands.Add(cmd);
            }
            FilterCommands();
        }

        /// <summary>
        /// Filtert die Commands basierend auf dem Suchtext
        /// </summary>
        private void FilterCommands()
        {
            FilteredCommands.Clear();

            var query = _allCommands.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLowerInvariant();
                query = query.Where(c =>
                    (c.Name?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                    (c.Category?.ToLowerInvariant().Contains(searchLower) ?? false));
            }

            foreach (var cmd in query.Take(100))
            {
                FilteredCommands.Add(cmd);
            }

            // Erstes Element automatisch auswaehlen
            if (FilteredCommands.Count > 0 && SelectedItem == null)
            {
                SelectedItem = FilteredCommands[0];
            }
        }

        /// <summary>
        /// Wiederholt das ausgewaehlte Command
        /// </summary>
        private async void ReplayCommand()
        {
            if (SelectedItem == null) return;

            OnCloseRequested?.Invoke();
            await CommandReplayManager.Instance.ReplayCommand(SelectedItem);
        }

        /// <summary>
        /// Loescht die Historie
        /// </summary>
        private void ClearHistory()
        {
            CommandReplayManager.Instance.ClearHistory();
            _allCommands.Clear();
            FilteredCommands.Clear();
        }
    }
}
