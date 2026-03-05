using Zenox.NET.Core.Common.Command;
using System.Collections.ObjectModel;

namespace Zenox.Maui.UI.Pages
{
    /// <summary>
    /// Daten-Seite - Data Tab
    /// </summary>
    public partial class DataPage : ContentPage
    {
        public RelayMenuGroup DataMenuGroup { get; set; }

        public DataPage()
        {
            Title = "📊 Daten";
            BindingContext = this;
            
            InitializeDataMenu();

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
                        Text = "Datenverwaltung",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Verwalten Sie Ihre Daten",
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

        private void InitializeDataMenu()
        {
            DataMenuGroup = new RelayMenuGroup { Name = "Daten" };

            var dataMenu = new RelayMenu { Name = "📁 Datenoperationen" };

            dataMenu.Add(new RelayObject
            {
                Name = "Liste anzeigen",
                Command = new RelayCommand(_ => DisplayAlert("Daten", "Datenliste anzeigen", "OK"))
                { Name = "Liste" }
            });

            dataMenu.Add(new RelayObject
            {
                Name = "Importieren",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "CSV Import",
                        Command = new RelayCommand(_ => DisplayAlert("Import", "CSV Datei importieren", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "JSON Import",
                        Command = new RelayCommand(_ => DisplayAlert("Import", "JSON Datei importieren", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "XML Import",
                        Command = new RelayCommand(_ => DisplayAlert("Import", "XML Datei importieren", "OK"))
                    }
                }
            });

            dataMenu.Add(new RelayObject
            {
                Name = "Exportieren",
                Children = new ObservableCollection<RelayObject>
                {
                    new RelayObject
                    {
                        Name = "CSV Export",
                        Command = new RelayCommand(_ => DisplayAlert("Export", "Als CSV exportieren", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "JSON Export",
                        Command = new RelayCommand(_ => DisplayAlert("Export", "Als JSON exportieren", "OK"))
                    },
                    new RelayObject
                    {
                        Name = "Excel Export",
                        Command = new RelayCommand(_ => DisplayAlert("Export", "Als Excel exportieren", "OK"))
                    }
                }
            });

            DataMenuGroup.Add(dataMenu);
        }
    }
}
