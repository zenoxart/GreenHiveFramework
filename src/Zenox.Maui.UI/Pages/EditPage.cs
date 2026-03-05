using Zenox.NET.Core.Common.Command;
using System.Collections.ObjectModel;

namespace Zenox.Maui.UI.Pages
{
    /// <summary>
    /// Bearbeiten-Seite - Edit Tab
    /// </summary>
    public partial class EditPage : ContentPage
    {
        public RelayMenuGroup EditMenuGroup { get; set; }

        public EditPage()
        {
            Title = "✏️ Bearbeiten";
            BindingContext = this;
            
            InitializeEditMenu();

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
                        Text = "Bearbeitungsfunktionen",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Rückgängig, Wiederholen und mehr",
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

        private void InitializeEditMenu()
        {
            EditMenuGroup = new RelayMenuGroup { Name = "Bearbeiten" };

            var editMenu = new RelayMenu { Name = "✏️ Aktionen" };

            editMenu.Add(new RelayObject
            {
                Name = "Rückgängig",
                Command = new RelayCommand(_ => DisplayAlert("Aktion", "Rückgängig", "OK"))
                { Name = "Rückgängig" }
            });

            editMenu.Add(new RelayObject
            {
                Name = "Wiederholen",
                Command = new RelayCommand(_ => DisplayAlert("Aktion", "Wiederholen", "OK"))
                { Name = "Wiederholen" }
            });

            editMenu.Add(new RelayObject
            {
                Name = "Zwischenablage",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "Ausschneiden",
                        Command = new RelayCommand(_ => DisplayAlert("Zwischenablage", "Ausschneiden", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "Kopieren",
                        Command = new RelayCommand(_ => DisplayAlert("Zwischenablage", "Kopieren", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "Einfügen",
                        Command = new RelayCommand(_ => DisplayAlert("Zwischenablage", "Einfügen", "OK"))
                    }
                }
            });

            editMenu.Add(new RelayObject
            {
                Name = "Suchen & Ersetzen",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "Suchen",
                        Command = new RelayCommand(_ => DisplayAlert("Suchen", "Suchfunktion öffnen", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "Ersetzen",
                        Command = new RelayCommand(_ => DisplayAlert("Ersetzen", "Ersetzen-Dialog öffnen", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "In Dateien suchen",
                        Command = new RelayCommand(_ => DisplayAlert("Suchen", "In allen Dateien suchen", "OK"))
                    }
                }
            });

            EditMenuGroup.Add(editMenu);
        }
    }
}
