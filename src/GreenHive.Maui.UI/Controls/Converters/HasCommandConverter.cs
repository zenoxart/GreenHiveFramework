using System.Globalization;

namespace GreenHive.Maui.UI.Controls
{
    /// <summary>
    /// Converter zur Überprüfung ob ein Wert nicht null oder leer ist
    /// </summary>
    public class HasCommandConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
                return false;
            
            if (value is string str)
                return !string.IsNullOrWhiteSpace(str);
            
            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
