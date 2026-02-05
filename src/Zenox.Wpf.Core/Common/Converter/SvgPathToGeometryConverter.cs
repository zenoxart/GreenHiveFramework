using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Linq;

namespace Zenox.NET.Core.Common.Converter
{
    public class SvgPathToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Geometry.Empty;

            // SVG-Datei laden und "d"-Attribut extrahieren
            var doc = XDocument.Load(path);
            var d = doc.Root?.Element("path")?.Attribute("d")?.Value;
            if (string.IsNullOrEmpty(d))
                return Geometry.Empty;

            return Geometry.Parse(d);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}