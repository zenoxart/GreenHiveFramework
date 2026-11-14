using System.Collections.ObjectModel;
using GreenHive.NET.Core.Common.Command;
using GreenHive.NET.Core.Common.MVVM.ViewModelSupport;

namespace GreenHive.Maui.UI.MVVM
{
    /// <summary>
    /// Manager für die Navigation und Menüstruktur der MAUI-Anwendung
    /// </summary>
    public class MenuManager : NotificationObject
    {
        private RelayMenuGroup? _navigationMenuGroup;
        private string? _currentPageTitle;

        /// <summary>
        /// Die Haupt-Menügruppe für die Navigation
        /// </summary>
        public RelayMenuGroup? NavigationMenuGroup
        {
            get => _navigationMenuGroup;
            set
            {
                _navigationMenuGroup = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Der Titel der aktuellen Seite
        /// </summary>
        public string? CurrentPageTitle
        {
            get => _currentPageTitle;
            set
            {
                _currentPageTitle = value;
                OnPropertyChanged();
            }
        }

        public MenuManager()
        {
            InitializeNavigationMenu();
        }

        /// <summary>
        /// Initialisiert das Navigationsmenü mit Standard-Einträgen
        /// </summary>
        private void InitializeNavigationMenu()
        {
            NavigationMenuGroup = new RelayMenuGroup
            {
                Name = "Navigation"
            };

            // Haupt-Menü
            var mainMenu = new RelayMenu { Name = "?? Haupt" };

            var homeItem = new RelayObject
            {
                Name = "Startseite",
                Command = new RelayCommand(_ => NavigateToPage("Startseite"))
                { Name = "Startseite" }
            };

            var dashboardItem = new RelayObject
            {
                Name = "Dashboard",
                Command = new RelayCommand(_ => NavigateToPage("Dashboard"))
                { Name = "Dashboard" }
            };

            var dataItem = new RelayObject
            {
                Name = "Daten",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "Liste",
                        Command = new RelayCommand(_ => NavigateToPage("Datenliste"))
                        { Name = "Liste" }
                    },
                    new RelayObject
                    {
                        Name = "Import",
                        Command = new RelayCommand(_ => NavigateToPage("Daten Import"))
                        { Name = "Import" }
                    },
                    new RelayObject
                    {
                        Name = "Export",
                        Command = new RelayCommand(_ => NavigateToPage("Daten Export"))
                        { Name = "Export" }
                    }
                }
            };

            mainMenu.Add(homeItem);
            mainMenu.Add(dashboardItem);
            mainMenu.Add(dataItem);

            // Werkzeuge-Menü
            var toolsMenu = new RelayMenu { Name = "?? Werkzeuge" };

            var settingsItem = new RelayObject
            {
                Name = "Einstellungen",
                Command = new RelayCommand(_ => NavigateToPage("Einstellungen"))
                { Name = "Einstellungen" }
            };

            var themeItem = new RelayObject
            {
                Name = "Design",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "Hell",
                        Command = new RelayCommand(_ => NavigateToPage("Helles Design"))
                        { Name = "Hell" }
                    },
                    new RelayObject
                    {
                        Name = "Dunkel",
                        Command = new RelayCommand(_ => NavigateToPage("Dunkles Design"))
                        { Name = "Dunkel" }
                    }
                }
            };

            var preferencesItem = new RelayObject
            {
                Name = "Präferenzen",
                Command = new RelayCommand(_ => NavigateToPage("Präferenzen"))
                { Name = "Präferenzen" }
            };

            toolsMenu.Add(settingsItem);
            toolsMenu.Add(themeItem);
            toolsMenu.Add(preferencesItem);

            // Hilfe-Menü
            var helpMenu = new RelayMenu { Name = "? Hilfe" };

            var aboutItem = new RelayObject
            {
                Name = "Über",
                Command = new RelayCommand(_ => NavigateToPage("Über"))
                { Name = "Über" }
            };

            var docsItem = new RelayObject
            {
                Name = "Dokumentation",
                Command = new RelayCommand(_ => NavigateToPage("Dokumentation"))
                { Name = "Dokumentation" }
            };

            var supportItem = new RelayObject
            {
                Name = "Support",
                Command = new RelayCommand(_ => NavigateToPage("Support"))
                { Name = "Support" }
            };

            helpMenu.Add(aboutItem);
            helpMenu.Add(docsItem);
            helpMenu.Add(supportItem);

            NavigationMenuGroup.Add(mainMenu);
            NavigationMenuGroup.Add(toolsMenu);
            NavigationMenuGroup.Add(helpMenu);
        }

        /// <summary>
        /// Fügt ein benutzerdefiniertes Menü zur Navigationsgruppe hinzu
        /// </summary>
        public void AddMenu(RelayMenu menu)
        {
            NavigationMenuGroup?.Add(menu);
        }

        /// <summary>
        /// Fügt einen Menüeintrag zu einem bestehenden Menü hinzu
        /// </summary>
        public void AddMenuItemToMenu(string menuName, RelayObject menuItem)
        {
            if (NavigationMenuGroup != null)
            {
                foreach (var menu in NavigationMenuGroup)
                {
                    if (menu.Name == menuName)
                    {
                        menu.Add(menuItem);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Navigiert zu einer bestimmten Seite
        /// </summary>
        private void NavigateToPage(string pageName)
        {
            CurrentPageTitle = pageName;
            
            // Hier können Sie die tatsächliche Navigation implementieren
            // z.B. mit Shell.Current.GoToAsync oder INavigationService
            System.Diagnostics.Debug.WriteLine($"Navigation zu: {pageName}");
            
            // Event für Navigation auslösen
            OnNavigationRequested(pageName);
        }

        /// <summary>
        /// Event das ausgelöst wird, wenn eine Navigation angefordert wird
        /// </summary>
        public event EventHandler<string>? NavigationRequested;

        protected virtual void OnNavigationRequested(string pageName)
        {
            NavigationRequested?.Invoke(this, pageName);
        }

        /// <summary>
        /// Löscht alle Menüs und initialisiert neu
        /// </summary>
        public void ResetMenu()
        {
            NavigationMenuGroup?.Clear();
            InitializeNavigationMenu();
        }
    }
}
