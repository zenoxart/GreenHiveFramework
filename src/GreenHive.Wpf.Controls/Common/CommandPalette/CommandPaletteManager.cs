using System.Windows.Input;
using GreenHive.NET.Core.Common.Command;
using GreenHive.NET.Core.Common.CommandPalette;

namespace GreenHive.Wpf.Controls.Common.CommandPalette
{
    /// <summary>
    /// Manager für die Command Palette - Zentrale Verwaltung aller verfügbaren Befehle
    /// </summary>
    public class CommandPaletteManager
    {
        private static CommandPaletteManager? _instance;
        private readonly List<CommandPaletteItem> _commands;
        private CommandPaletteWindow? _paletteWindow;

        private CommandPaletteManager()
        {
            _commands = new List<CommandPaletteItem>();
        }

        /// <summary>
        /// Singleton-Instanz des Managers
        /// </summary>
        public static CommandPaletteManager Instance => _instance ??= new CommandPaletteManager();

        /// <summary>
        /// Registriert einen neuen Befehl in der Palette
        /// </summary>
        public void RegisterCommand(string name, string category, ICommand command, 
            string? description = null, string? iconPath = null, string? shortcut = null, 
            object? commandParameter = null, params string[] keywords)
        {
            var item = new CommandPaletteItem
            {
                Name = name,
                Description = description,
                Category = category,
                IconPath = iconPath,
                Shortcut = shortcut,
                Command = command,
                CommandParameter = commandParameter,
                Keywords = keywords
            };

            _commands.Add(item);
        }

        /// <summary>
        /// Registriert einen neuen Befehl mit Action
        /// </summary>
        public void RegisterCommand(string name, string category, Action action,
            string? description = null, string? iconPath = null, string? shortcut = null,
            params string[] keywords)
        {
            var command = new RelayCommand(_ => action(), _ => true);
            RegisterCommand(name, category, command, description, iconPath, shortcut, null, keywords);
        }

        /// <summary>
        /// Entfernt einen Befehl aus der Palette
        /// </summary>
        public void UnregisterCommand(string name)
        {
            _commands.RemoveAll(c => c.Name == name);
        }

        /// <summary>
        /// Entfernt alle Befehle einer Kategorie
        /// </summary>
        public void UnregisterCategory(string category)
        {
            _commands.RemoveAll(c => c.Category == category);
        }

        /// <summary>
        /// Gibt alle registrierten Befehle zurück
        /// </summary>
        public IReadOnlyList<CommandPaletteItem> GetAllCommands()
        {
            return _commands.AsReadOnly();
        }

        /// <summary>
        /// Öffnet die Command Palette
        /// </summary>
        public void ShowPalette()
        {
            // Wenn bereits offen, Fokus setzen
            if (_paletteWindow != null && _paletteWindow.IsVisible)
            {
                _paletteWindow.Activate();
                return;
            }

            // Neue Palette erstellen
            _paletteWindow = new CommandPaletteWindow();
            var viewModel = new CommandPaletteViewModel();
            
            // Alle registrierten Befehle hinzufügen
            viewModel.AddCommands(_commands);
            
            _paletteWindow.ViewModel = viewModel;
            _paletteWindow.Closed += (s, e) => _paletteWindow = null;
            
            _paletteWindow.Show();
        }

        /// <summary>
        /// Schließt die Command Palette
        /// </summary>
        public void ClosePalette()
        {
            _paletteWindow?.Close();
            _paletteWindow = null;
        }

        /// <summary>
        /// Entfernt alle registrierten Befehle
        /// </summary>
        public void ClearAllCommands()
        {
            _commands.Clear();
        }
    }
}
