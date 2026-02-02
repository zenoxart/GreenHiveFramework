using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Zenox.Wpf.Core.Common.CommandHistory;

namespace Zenox.Wpf.Controls.Common.CommandHistory
{
    /// <summary>
    /// Command History Overlay - zeigt die ausgefuehrten Commands aehnlich der Command Palette
    /// </summary>
    public partial class CommandHistoryOverlay : Window
    {
        private readonly CommandHistoryViewModel _viewModel;
        private bool _isClosing;

        public CommandHistoryOverlay()
        {
            InitializeComponent();

            _viewModel = new CommandHistoryViewModel();
            _viewModel.OnCloseRequested = () => SafeClose();
            DataContext = _viewModel;

            Loaded += (s, e) =>
            {
                SearchBox.Focus();
                _viewModel.RefreshHistory();
            };

            Deactivated += (s, e) =>
            {
                // Schliessen wenn Fokus verloren (nur wenn nicht bereits am Schliessen)
                SafeClose();
            };

            Closing += (s, e) =>
            {
                _isClosing = true;
            };
        }

        private void SafeClose()
        {
            if (!_isClosing && IsLoaded)
            {
                _isClosing = true;
                try
                {
                    Close();
                }
                catch (InvalidOperationException)
                {
                    // Fenster wird bereits geschlossen - ignorieren
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    SafeClose();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (_viewModel.SelectedItem != null)
                    {
                        _viewModel.ReplaySelectedCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    break;

                case Key.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    break;
            }
        }

        private void MoveSelection(int direction)
        {
            if (_viewModel.FilteredCommands.Count == 0) return;

            var currentIndex = _viewModel.FilteredCommands.IndexOf(_viewModel.SelectedItem);
            var newIndex = currentIndex + direction;

            if (newIndex < 0) newIndex = _viewModel.FilteredCommands.Count - 1;
            if (newIndex >= _viewModel.FilteredCommands.Count) newIndex = 0;

            _viewModel.SelectedItem = _viewModel.FilteredCommands[newIndex];
            HistoryList.ScrollIntoView(_viewModel.SelectedItem);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
        }

        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedItem != null)
            {
                _viewModel.ReplaySelectedCommand.Execute(null);
            }
        }
    }

    /// <summary>
    /// Konvertiert Timestamp zu relativer Zeit
    /// </summary>
    public class TimestampConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime timestamp)
            {
                var diff = DateTime.UtcNow - timestamp;

                if (diff.TotalSeconds < 60)
                    return $"{(int)diff.TotalSeconds}s";
                if (diff.TotalMinutes < 60)
                    return $"{(int)diff.TotalMinutes}m";
                if (diff.TotalHours < 24)
                    return $"{(int)diff.TotalHours}h";

                return timestamp.ToLocalTime().ToString("dd.MM HH:mm");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konvertiert Success zu Farbe
    /// </summary>
    public class SuccessToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is true ? "#4CAF50" : "#FF6B6B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konvertiert Textlaenge zu Visibility
    /// </summary>
    public class TextLengthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int length)
            {
                return length == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
