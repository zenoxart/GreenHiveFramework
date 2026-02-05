using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zenox.Wpf.Controls.Common.Dialogs
{
    /// <summary>
    /// Einfacher Eingabedialog für Textabfragen.
    /// </summary>
    public class InputDialog : System.Windows.Window
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
            ResizeMode = System.Windows.ResizeMode.NoResize;
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#1E1E1E"));

            var grid = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(20) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });

            var promptText = new TextBlock
            {
                Text = prompt,
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            System.Windows.Controls.Grid.SetRow(promptText, 0);

            _textBox = new TextBox
            {
                Text = defaultValue,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#2D2D2D")),
                Foreground = Brushes.White,
                Padding = new System.Windows.Thickness(8),
                FontSize = 14,
                BorderThickness = new System.Windows.Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#3A3A3A"))
            };
            System.Windows.Controls.Grid.SetRow(_textBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(0, 15, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 80,
                Margin = new System.Windows.Thickness(0, 0, 10, 0),
                Padding = new System.Windows.Thickness(0, 8, 0, 8),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.MediumPurple),
                Foreground = Brushes.White,
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            okButton.Click += (s, e) => { DialogResult = true; Close(); };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Abbrechen",
                Width = 80,
                Padding = new System.Windows.Thickness(0, 8, 0, 8),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#2D2D2D")),
                Foreground = Brushes.White,
                BorderThickness = new System.Windows.Thickness(0),
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
