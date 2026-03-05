using Zenox.NET.Core.Common.Command;
using System.Collections.ObjectModel;

namespace Zenox.Maui.UI.Pages
{
    /// <summary>
    /// Werkzeuge-Seite - Tools Tab
    /// </summary>
    public partial class ToolsPage : ContentPage
    {
        public RelayMenuGroup ToolsMenuGroup { get; set; }

        public ToolsPage()
        {
            Title = "🔧 Werkzeuge";
            BindingContext = this;
            
            InitializeToolsMenu();

            var grid = new Grid
            {
                Padding = 20,
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star }
                }
            };

            var header = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Werkzeuge & Einstellungen",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Konfigurieren Sie Ihre Anwendung",
                        FontSize = 14,
                        Opacity = 0.7
                    },
                    new BoxView
                    {
                        HeightRequest = 2,
                        Color = Colors.Gray,
                        Opacity = 0.3,
                        Margin = new Thickness(0, 10)
                    }
                }
            };

            
            Grid.SetRow(header, 0);

            grid.Children.Add(header);

            Content = grid;
        }

        private void InitializeToolsMenu()
        {
            ToolsMenuGroup = new RelayMenuGroup { Name = "Werkzeuge" };

            var settingsMenu = new RelayMenu { Name = "⚙️ Einstellungen" };

            settingsMenu.Add(new RelayObject
            {
                Name = "Allgemein",
                Command = new RelayCommand(_ => DisplayAlert("Einstellungen", "Allgemeine Einstellungen", "OK"))
                { Name = "Allgemein" }
            });

            settingsMenu.Add(new RelayObject
            {
                Name = "Darstellung",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "Helles Design",
                        Command = new RelayCommand(_ => DisplayAlert("Design", "Helles Design aktiviert", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "Dunkles Design",
                        Command = new RelayCommand(_ => DisplayAlert("Design", "Dunkles Design aktiviert", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "System-Standard",
                        Command = new RelayCommand(_ => DisplayAlert("Design", "System-Design aktiviert", "OK"))
                    }
                }
            });

            settingsMenu.Add(new RelayObject
            {
                Name = "Sprache",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "Deutsch",
                        Command = new RelayCommand(_ => DisplayAlert("Sprache", "Deutsch ausgewählt", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "English",
                        Command = new RelayCommand(_ => DisplayAlert("Language", "English selected", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "Français",
                        Command = new RelayCommand(_ => DisplayAlert("Langue", "Français sélectionné", "OK"))
                    }
                }
            });

            var toolsMenu = new RelayMenu { Name = "🛠️ Dienstprogramme" };

            toolsMenu.Add(new RelayObject
            {
                Name = "Cache leeren",
                Command = new RelayCommand(_ => DisplayAlert("Cache", "Cache wurde geleert", "OK"))
                { Name = "Cache" }
            });

            toolsMenu.Add(new RelayObject
            {
                Name = "Diagnose",
                Command = new RelayCommand(_ => DisplayAlert("Diagnose", "Diagnose-Tools öffnen", "OK"))
                { Name = "Diagnose" }
            });

            ToolsMenuGroup.Add(settingsMenu);
            ToolsMenuGroup.Add(toolsMenu);
        }
    }
}
