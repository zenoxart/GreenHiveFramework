using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zenox.Wpf.Controls.Common.Dialogs
{
    /// <summary>
    /// Einfacher Eingabedialog für Textabfragen.
    /// </summary>
    public class InputDialog : Window
    {
        private readonly TextBox _textBox;

        /// <summary>
        /// Der eingegebene Text.
        /// </summary>
        public string InputText => _textBox.Text;

        /// <summary>
        /// Erstellt einen neuen Eingabedialog.
        /// </summary>
        /// <param name="title">Titel des Dialogs</param>
        /// <param name="prompt">Eingabeaufforderung</param>
        /// <param name="defaultValue">Optionaler Standardwert</param>
        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            var promptText = new TextBlock
            {
                Text = prompt,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(promptText, 0);

            _textBox = new TextBox
            {
                Text = defaultValue,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D")),
                Foreground = Brushes.White,
                Padding = new Thickness(8),
                FontSize = 14,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3A"))
            };
            Grid.SetRow(_textBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(buttonPanel, 3);

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(0, 8, 0, 8),
                Background = new SolidColorBrush(Colors.MediumPurple),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            okButton.Click += (s, e) => { DialogResult = true; Close(); };

            var cancelButton = new Button
            {
                Content = "Abbrechen",
                Width = 80,
                Padding = new Thickness(0, 8, 0, 8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelButton.Click += (s, e) => Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(promptText);
            grid.Children.Add(_textBox);
            grid.Children.Add(buttonPanel);

            Content = grid;

            Loaded += (s, e) => _textBox.Focus();
        }
    }
}
