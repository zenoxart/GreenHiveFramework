using System.Globalization;
using System.Windows.Data;

namespace GreenHive.Wpf.Controls.Common.TabView.Converters
{
    /// <summary>
    /// Converter für inverse Boolean-Werte
    /// </summary>
    public class InverseBoolConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
