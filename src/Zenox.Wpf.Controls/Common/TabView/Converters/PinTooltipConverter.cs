using System.Globalization;
using System.Windows.Data;

namespace Zenox.Wpf.Controls.Common.TabView.Converters
{
    /// <summary>
    /// Converter für Pin-Tooltip
    /// </summary>
    public class PinTooltipConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Unpin Tab" : "Pin Tab";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
