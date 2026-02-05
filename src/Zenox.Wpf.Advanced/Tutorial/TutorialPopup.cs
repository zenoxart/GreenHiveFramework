using System.Windows;
using System.Windows.Controls;

namespace Zenox.NET.Core.Common.Tutorial
{
    /// <summary>
    /// Definiert ein Tooltip-Popup für Tutorial-Punkte
    /// </summary>
    public class TutorialPopup : UserControl
    {
        private TextBlock _titleText;
        private TextBlock _contentText;
        private TextBlock _progressText;
        private Button _nextButton;
        private Button _previousButton;
        private Button _skipButton;
        private Button _closeButton;

        public event EventHandler? NextClicked;
        public event EventHandler? PreviousClicked;
        public event EventHandler? SkipClicked;
        public event EventHandler? CloseClicked;

        public TutorialPopup()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Width = 300;
            MaxWidth = 400;

            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White, // Solid black background
                BorderBrush = System.Windows.Media.Brushes.Gray, // White border for better contrast
                BorderThickness = new Thickness(2, 2, 2, 2), // Slightly thicker border
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 16, 16, 16),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    Direction = 315,
                    ShadowDepth = 5,
                    Opacity = 0.8,
                    BlurRadius = 10
                }
            };

            var stackPanel = new StackPanel();

            // Header mit Titel und Schließen-Button
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title-Text
            _titleText = new TextBlock
            {
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = System.Windows.Media.Brushes.Black // White text on black background
            };
            Grid.SetColumn(_titleText, 0);

            // Close-Button
            _closeButton = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 0),
                FontSize = 7,
                Cursor = System.Windows.Input.Cursors.Hand,
                Foreground = System.Windows.Media.Brushes.Black // White X button
            };
            _closeButton.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(_closeButton, 1);

            headerGrid.Children.Add(_titleText);
            headerGrid.Children.Add(_closeButton);

            // Content
            _contentText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16),
                LineHeight = 20,
                Foreground = System.Windows.Media.Brushes.Black // White text on black background
            };

            // Progress
            _progressText = new TextBlock
            {
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.LightGray // Light gray for progress text
            };

            // Buttons-Panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Zurück-Button
            _previousButton = new Button
            {
                Content = "Zurück",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = System.Windows.Media.Brushes.DarkGray,
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _previousButton.Click += (s, e) => PreviousClicked?.Invoke(this, EventArgs.Empty);
            // Hover-Effekt für Previous Button
            _previousButton.MouseEnter += (s, e) => _previousButton.Background = System.Windows.Media.Brushes.Gray;
            _previousButton.MouseLeave += (s, e) => _previousButton.Background = System.Windows.Media.Brushes.DarkGray;

            // Überspringen-Button
            _skipButton = new Button
            {
                Content = "Überspringen",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = System.Windows.Media.Brushes.DarkGray,
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _skipButton.Click += (s, e) => SkipClicked?.Invoke(this, EventArgs.Empty);
            // Hover-Effekt für Skip Button
            _skipButton.MouseEnter += (s, e) => _skipButton.Background = System.Windows.Media.Brushes.Gray;
            _skipButton.MouseLeave += (s, e) => _skipButton.Background = System.Windows.Media.Brushes.DarkGray;

            // Nächster-Schritt-Button
            _nextButton = new Button
            {
                Content = "Weiter",
                Padding = new Thickness(12, 6, 12, 6),
                Background = System.Windows.Media.Brushes.SteelBlue,
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.SteelBlue,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            _nextButton.Click += (s, e) => NextClicked?.Invoke(this, EventArgs.Empty);
            // Hover-Effekt für Next Button
            _nextButton.MouseEnter += (s, e) => _nextButton.Background = System.Windows.Media.Brushes.Blue;
            _nextButton.MouseLeave += (s, e) => _nextButton.Background = System.Windows.Media.Brushes.SteelBlue;

            // Buttons dem Panel hinzufügen
            buttonPanel.Children.Add(_previousButton);
            buttonPanel.Children.Add(_skipButton);
            buttonPanel.Children.Add(_nextButton);

            // Content-Elemente dem Stackpanel hinzufügen
            stackPanel.Children.Add(headerGrid);
            stackPanel.Children.Add(_contentText);
            stackPanel.Children.Add(_progressText);
            stackPanel.Children.Add(buttonPanel);

            border.Child = stackPanel;
            Content = border;
        }

        /// <summary>
        /// Aktualisiert den Inhalt
        /// </summary>
        /// <param name="title">Titel des Tutorial-Schrittes</param>
        /// <param name="content">Textinhalt des Tutorial-Schrittes</param>
        /// <param name="currentStep">Index des Aktuellen Tutorial-Schrittes</param>
        /// <param name="totalSteps">Gesammtanzahl der Tutorial-Schritte</param>
        /// <param name="canGoPrevious">Wahrheitswert ob man einen Schritt zurück gehen kann</param>
        /// <param name="canSkip">Wahrheitswert, ob das Tutorial übersprungen werden kann</param>
        public void UpdateContent(string title, string content, int currentStep, int totalSteps,
            bool canGoPrevious, bool canSkip)
        {
            _titleText.Text = title ?? "Tutorial";
            _contentText.Text = content ?? "";
            _progressText.Text = $"Schritt {currentStep} von {totalSteps}";

            _previousButton.Visibility = canGoPrevious ? Visibility.Visible : Visibility.Collapsed;
            _skipButton.Visibility = canSkip ? Visibility.Visible : Visibility.Collapsed;
            _nextButton.Content = currentStep == totalSteps ? "Fertig" : "Weiter";
        }
    }
}