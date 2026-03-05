using System.Collections;
using System.Globalization;

namespace Zenox.Maui.UI.Controls
{
    /// <summary>
    /// Converter zur Überprüfung ob eine Collection Elemente enthält
    /// </summary>
    public class HasChildrenConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ICollection collection)
            {
                bool hasChildren = collection.Count > 0;
                
                // Unterstützung für inverse Logik durch Parameter
                if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                {
                    return !hasChildren;
                }
                
                return hasChildren;
            }
            
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
