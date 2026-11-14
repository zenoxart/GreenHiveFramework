using GreenHive.NET.Core.Common.Command;
using GreenHive.NET.Core.Common.MVVM.ViewModelSupport;

namespace GreenHive.Maui.UI.Controls
{
    /// <summary>
    /// Beispiel-ViewModel zur Demonstration der RelayMenuControl Verwendung
    /// </summary>
    public class RelayMenuExampleViewModel : NotificationObject
    {
        private RelayMenuGroup? _menuGroup;

        public RelayMenuGroup? MenuGroup
        {
            get => _menuGroup;
            set
            {
                _menuGroup = value;
                OnPropertyChanged();
            }
        }

        public RelayMenuExampleViewModel()
        {
            InitializeMenus();
        }

        private void InitializeMenus()
        {
            // Erstellen einer MenuGroup mit mehreren Menus
            MenuGroup = new RelayMenuGroup
            {
                Name = "Hauptmenü"
            };

            // Datei-Menü
            var fileMenu = new RelayMenu
            {
                Name = "Datei"
            };

            // Neue Datei mit Untermenüs
            var newFileItem = new RelayObject
            {
                Name = "Neu",
                PngPath = "new_file.png",
                Children = new System.Collections.ObjectModel.ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "Projekt",
                        Command = new RelayCommand(_ => System.Diagnostics.Debug.WriteLine("Neues Projekt erstellen"))
                        {
                            Name = "Projekt"
                        }
                    },
                    new RelayObject
                    {
                        Name = "Datei",
                        Command = new RelayCommand(_ => System.Diagnostics.Debug.WriteLine("Neue Datei erstellen"))
                        {
                            Name = "Datei"
                        }
                    }
                }
            };

            var openItem = new RelayObject
            {
                Name = "Öffnen",
                PngPath = "open_file.png",
                Command = new RelayCommand(_ => System.Diagnostics.Debug.WriteLine("Datei öffnen"))
                {
                    Name = "Öffnen"
                }
            };

            var saveItem = new RelayObject
            {
                Name = "Speichern",
                PngPath = "save_file.png",
                Command = new RelayCommand(_ => System.Diagnostics.Debug.WriteLine("Datei speichern"))
                {
                    Name = "Speichern"
                }
            };

            fileMenu.Add(newFileItem);
            fileMenu.Add(openItem);
            fileMenu.Add(saveItem);

            // Bearbeiten-Menü
            var editMenu = new RelayMenu
            {
                Name = "Bearbeiten"
            };

            var undoItem = new RelayObject
            {
                Name = "Rückgängig",
                PngPath = "undo.png",
                Command = new RelayCommand(_ => System.Diagnostics.Debug.WriteLine("Rückgängig"))
                {
                    Name = "Rückgängig"
                }
            };

            var redoItem = new RelayObject
            {
                Name = "Wiederholen",
                PngPath = "redo.png",
                Command = new RelayCommand(_ => System.Diagnostics.Debug.WriteLine("Wiederholen"))
                {
                    Name = "Wiederholen"
                }
            };

            editMenu.Add(undoItem);
            editMenu.Add(redoItem);

            // Menus zur Gruppe hinzufügen
            MenuGroup.Add(fileMenu);
            MenuGroup.Add(editMenu);
        }
    }
}
