using Zenox.NET.Core.Common.Command;
using System.Collections.ObjectModel;

namespace Zenox.Maui.UI.Pages
{
    /// <summary>
    /// Startseite - Home Tab
    /// </summary>
    public partial class HomePage : ContentPage
    {
        public HomePage()
        {
            Title = "?? Start";
            
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 20,
                    Spacing = 20,
                    Children =
                    {
                        new Label
                        {
                            Text = "Willkommen bei Zenox.NET",
                            FontSize = 28,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = "MAUI Framework mit RelayMenu Integration",
                            FontSize = 16,
                            HorizontalOptions = LayoutOptions.Center,
                            Opacity = 0.7
                        },
                        new BoxView
                        {
                            HeightRequest = 2,
                            Color = Colors.Gray,
                            Opacity = 0.3,
                            Margin = new Thickness(0, 10)
                        },
                        new Label
                        {
                            Text = "Features:",
                            FontSize = 20,
                            FontAttributes = FontAttributes.Bold,
                            Margin = new Thickness(0, 10, 0, 0)
                        },
                        new Label { Text = "? Hierarchische Menüstrukturen" },
                        new Label { Text = "? RelayCommand Pattern" },
                        new Label { Text = "? MVVM Architektur" },
                        new Label { Text = "? AppKontext für Services" },
                        new Label { Text = "? TabbedPage Navigation" },
                        new Label { Text = "? Responsive Design" },
                        new Button
                        {
                            Text = "Mehr erfahren",
                            Margin = new Thickness(0, 20, 0, 0),
                            Command = new RelayCommand(_ => 
                                Application.Current?.MainPage?.DisplayAlert(
                                    "Information", 
                                    "Dies ist das Zenox.NET MAUI Framework", 
                                    "OK"))
                        }
                    }
                }
            };
        }
    }
}
