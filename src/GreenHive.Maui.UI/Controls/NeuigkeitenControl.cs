using GreenHive.NET.Core.Common.Neuigkeiten;
using GreenHive.Maui.UI.ViewModels;

namespace GreenHive.Maui.UI.Controls
{
    /// <summary>
    /// Control zur Anzeige von Neuigkeiten/What's New
    /// </summary>
    public class NeuigkeitenControl : ContentView
    {
        private readonly NeuigkeitenViewModel _viewModel;
        private readonly ScrollView _scrollView;
        private readonly VerticalStackLayout _contentLayout;

        public NeuigkeitenControl()
        {
            _viewModel = new NeuigkeitenViewModel();
            BindingContext = _viewModel;

            _contentLayout = new VerticalStackLayout
            {
                Spacing = 10,
                Padding = new Thickness(20)
            };

            _scrollView = new ScrollView
            {
                Content = _contentLayout
            };

            Content = _scrollView;

            // Reagiere auf Konfigurationsänderungen
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NeuigkeitenViewModel.Konfiguration))
            {
                BuildUI();
            }
        }

        private void BuildUI()
        {
            _contentLayout.Children.Clear();

            if (_viewModel.Konfiguration == null)
                return;

            // Header
            _contentLayout.Children.Add(new Label
            {
                Text = _viewModel.Konfiguration.Titel,
                FontSize = 32,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            if (!string.IsNullOrEmpty(_viewModel.Konfiguration.Untertitel))
            {
                _contentLayout.Children.Add(new Label
                {
                    Text = _viewModel.Konfiguration.Untertitel,
                    FontSize = 16,
                    Opacity = 0.7,
                    Margin = new Thickness(0, 0, 0, 20)
                });
            }

            // Versionen
            foreach (var version in _viewModel.Konfiguration.Versionen)
            {
                _contentLayout.Children.Add(CreateVersionView(version));
            }
        }

        private View CreateVersionView(VersionNeuigkeiten version)
        {
            var versionLayout = new VerticalStackLayout
            {
                Spacing = 15,
                Margin = new Thickness(0, 0, 0, 30)
            };

            // Version Header
            var headerLayout = new HorizontalStackLayout
            {
                Spacing = 10
            };

            headerLayout.Children.Add(new Label
            {
                Text = $"Version {version.Version}",
                FontSize = 24,
                FontAttributes = FontAttributes.Bold
            });

            if (!string.IsNullOrEmpty(version.ReleaseTyp))
            {
                headerLayout.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#007ACC"),
                    Padding = new Thickness(8, 4),
                    StrokeThickness = 0,
                    Content = new Label
                    {
                        Text = version.ReleaseTyp,
                        TextColor = Colors.White,
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold
                    }
                });
            }

            versionLayout.Children.Add(headerLayout);

            // Datum
            if (version.Datum.HasValue)
            {
                versionLayout.Children.Add(new Label
                {
                    Text = $"Veröffentlicht am: {version.DatumFormatiert}",
                    FontSize = 14,
                    Opacity = 0.6
                });
            }

            // Separator
            versionLayout.Children.Add(new BoxView
            {
                HeightRequest = 2,
                Color = Colors.Gray,
                Opacity = 0.3,
                Margin = new Thickness(0, 10)
            });

            // Kategorien
            foreach (var kategorie in version.Kategorien)
            {
                versionLayout.Children.Add(CreateKategorieView(kategorie));
            }

            return versionLayout;
        }

        private View CreateKategorieView(NeuigkeitenKategorie kategorie)
        {
            var kategorieLayout = new VerticalStackLayout
            {
                Spacing = 10,
                Margin = new Thickness(0, 10, 0, 20)
            };

            // Kategorie Header
            var headerLayout = new HorizontalStackLayout
            {
                Spacing = 8
            };

            if (!string.IsNullOrEmpty(kategorie.Icon))
            {
                headerLayout.Children.Add(new Label
                {
                    Text = kategorie.Icon,
                    FontSize = 20,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            headerLayout.Children.Add(new Label
            {
                Text = kategorie.Titel,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            });

            kategorieLayout.Children.Add(headerLayout);

            // Einträge
            foreach (var eintrag in kategorie.Eintraege)
            {
                kategorieLayout.Children.Add(CreateEintragView(eintrag));
            }

            return kategorieLayout;
        }

        private View CreateEintragView(NeuigkeitEintrag eintrag)
        {
            var eintragLayout = new VerticalStackLayout
            {
                Spacing = 5,
                Margin = new Thickness(20, 5, 0, 10)
            };

            // Eintrag Text mit Icon
            var textLayout = new HorizontalStackLayout
            {
                Spacing = 8
            };

            textLayout.Children.Add(new Label
            {
                Text = "•",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 2, 0, 0)
            });

            if (!string.IsNullOrEmpty(eintrag.Icon))
            {
                textLayout.Children.Add(new Label
                {
                    Text = eintrag.Icon,
                    FontSize = 16,
                    VerticalOptions = LayoutOptions.Start
                });
            }

            textLayout.Children.Add(new Label
            {
                Text = eintrag.Text,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Start
            });

            eintragLayout.Children.Add(textLayout);

            // Beschreibung
            if (!string.IsNullOrEmpty(eintrag.Beschreibung))
            {
                eintragLayout.Children.Add(new Label
                {
                    Text = eintrag.Beschreibung,
                    FontSize = 14,
                    Opacity = 0.7,
                    Margin = new Thickness(24, 0, 0, 0)
                });
            }

            // Link
            if (!string.IsNullOrEmpty(eintrag.Link))
            {
                var linkLabel = new Label
                {
                    Text = "Mehr erfahren →",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#007ACC"),
                    TextDecorations = TextDecorations.Underline,
                    Margin = new Thickness(24, 5, 0, 0)
                };

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += async (s, e) =>
                {
                    try
                    {
                        await Launcher.OpenAsync(new Uri(eintrag.Link));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Fehler beim Öffnen des Links: {ex.Message}");
                    }
                };

                linkLabel.GestureRecognizers.Add(tapGesture);
                eintragLayout.Children.Add(linkLabel);
            }

            return eintragLayout;
        }
    }
}
