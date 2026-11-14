using GreenHive.NET.Core.Common.Command;
using System.Collections.ObjectModel;

namespace GreenHive.Maui.UI.Pages
{
    /// <summary>
    /// Hilfe-Seite - Help Tab mit Neuigkeiten, Dokumentation und Support
    /// </summary>
    public partial class HelpPage : ContentPage
    {
        public HelpPage()
        {
            Title = "? Hilfe";
            
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 20,
                    Spacing = 20,
                    Children =
                    {
                        // Tabs für verschiedene Bereiche
                        CreateTabButtons(),
                        
                        new BoxView
                        {
                            HeightRequest = 2,
                            Color = Colors.Gray,
                            Opacity = 0.3
                        },
                        
                        // Content Container
                        CreateContentContainer()
                    }
                }
            };
        }

        private View CreateTabButtons()
        {
            var tabLayout = new HorizontalStackLayout
            {
                Spacing = 10,
                HorizontalOptions = LayoutOptions.Center
            };

            var neuigkeitenButton = CreateTabButton("? Neuigkeiten", true);
            neuigkeitenButton.Clicked += (s, e) => ShowNeuigkeiten();
            
            var docsButton = CreateTabButton("?? Doku", false);
            docsButton.Clicked += (s, e) => ShowDokumentation();
            
            var supportButton = CreateTabButton("?? Support", false);
            supportButton.Clicked += (s, e) => ShowSupport();

            tabLayout.Children.Add(neuigkeitenButton);
            tabLayout.Children.Add(docsButton);
            tabLayout.Children.Add(supportButton);

            return tabLayout;
        }

        private Button CreateTabButton(string text, bool isActive)
        {
            return new Button
            {
                Text = text,
                FontSize = 14,
                Padding = new Thickness(20, 10),
                BackgroundColor = isActive ? Color.FromArgb("#007ACC") : Colors.Transparent,
                TextColor = isActive ? Colors.White : Color.FromArgb("#007ACC"),
                BorderColor = Color.FromArgb("#007ACC"),
                BorderWidth = 1,
                CornerRadius = 5
            };
        }

        private VerticalStackLayout _contentContainer;

        private View CreateContentContainer()
        {
            _contentContainer = new VerticalStackLayout
            {
                Spacing = 20
            };

            // Standard: Neuigkeiten anzeigen
            ShowNeuigkeiten();

            return _contentContainer;
        }

        private void ShowNeuigkeiten()
        {
            _contentContainer.Children.Clear();
            _contentContainer.Children.Add(new Controls.NeuigkeitenControl());
        }

        private void ShowDokumentation()
        {
            _contentContainer.Children.Clear();
            
            var docsLayout = new VerticalStackLayout
            {
                Spacing = 20,
                Children =
                {
                    new Label
                    {
                        Text = "?? Dokumentation",
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold
                    },
                    new BoxView
                    {
                        HeightRequest = 2,
                        Color = Colors.Gray,
                        Opacity = 0.3
                    },
                    new Label
                    {
                        Text = "Über Green Hive",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 10, 0, 0)
                    },
                    new Label
                    {
                        Text = "Version: 1.0.0\nFramework: .NET MAUI 10.0\nArchitektur: MVVM mit RelayCommand Pattern",
                        LineHeight = 1.5
                    },
                    new Label
                    {
                        Text = "Ressourcen",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 20, 0, 0)
                    },
                    new Button
                    {
                        Text = "?? Online-Dokumentation öffnen",
                        Command = new RelayCommand(_ => 
                            Launcher.OpenAsync(new Uri("https://github.com/zenoxart/Green Hive")))
                    },
                    new Button
                    {
                        Text = "?? Video-Tutorials",
                        Command = new RelayCommand(_ => 
                            DisplayAlert("Tutorials", "Video-Tutorials verfügbar", "OK"))
                    },
                    new Button
                    {
                        Text = "?? Beispiele & Demos",
                        Command = new RelayCommand(_ => 
                            DisplayAlert("Beispiele", "Code-Beispiele verfügbar", "OK"))
                    }
                }
            };

            _contentContainer.Children.Add(docsLayout);
        }

        private void ShowSupport()
        {
            _contentContainer.Children.Clear();
            
            var supportLayout = new VerticalStackLayout
            {
                Spacing = 20,
                Children =
                {
                    new Label
                    {
                        Text = "?? Support & Community",
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold
                    },
                    new BoxView
                    {
                        HeightRequest = 2,
                        Color = Colors.Gray,
                        Opacity = 0.3
                    },
                    new Label
                    {
                        Text = "Hilfe benötigt?",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 10, 0, 0)
                    },
                    new Button
                    {
                        Text = "?? Community Forum",
                        Command = new RelayCommand(_ => 
                            DisplayAlert("Forum", "Besuchen Sie unser Community Forum", "OK"))
                    },
                    new Button
                    {
                        Text = "?? Bug melden",
                        Command = new RelayCommand(_ => 
                            Launcher.OpenAsync(new Uri("https://github.com/zenoxart/GreenHive.NET/issues")))
                    },
                    new Button
                    {
                        Text = "?? Feature Request",
                        Command = new RelayCommand(_ => 
                            DisplayAlert("Feature", "Schlagen Sie neue Features vor", "OK"))
                    },
                    new Button
                    {
                        Text = "?? Support kontaktieren",
                        Command = new RelayCommand(_ => 
                            DisplayAlert("Support", "Kontaktieren Sie uns unter: support@greenhive.net", "OK"))
                    },
                    new Label
                    {
                        Text = "Lizenz",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 20, 0, 0)
                    },
                    new Label
                    {
                        Text = "MIT License\n© 2024 GreenHive.NET\nAlle Rechte vorbehalten.",
                        LineHeight = 1.5,
                        Opacity = 0.7
                    }
                }
            };

            _contentContainer.Children.Add(supportLayout);
        }
    }
}
