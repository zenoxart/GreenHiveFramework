using System.Globalization;
using System.Windows.Data;

namespace Zenox.Wpf.Controls.Common.TabView.Converters
{
    /// <summary>
    /// Converter für Pin-Icon
    /// </summary>
    public class PinIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "📌" : "📍";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
