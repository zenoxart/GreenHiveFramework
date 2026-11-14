using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace GreenHive.Wpf.Controls.Common.Heatmap
{
    /// <summary>
    /// Interaktionslogik fuer InteractionHeatmapView.xaml
    /// </summary>
    public partial class InteractionHeatmapView : UserControl
    {
        public InteractionHeatmapView()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Konverter fuer Heatmap-Intensitaet zu Farbe und anderen Werten
    /// </summary>
    public class IntensityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var param = parameter?.ToString() ?? "";

            switch (param)
            {
                case "Position":
                    // Grid-Position zu Canvas-Position (multipliziert mit Zellengroesse)
                    if (value is int gridPos)
                    {
                        return (double)(gridPos * 20); // Default CellSize
                    }
                    return 0.0;

                case "Size":
                    // Count zu Groesse
                    if (value is int count)
                    {
                        return Math.Min(50, Math.Max(10, count * 3));
                    }
                    return 20.0;

                case "EmptyVisibility":
                    // Count zu Visibility
                    if (value is int itemCount)
                    {
                        return itemCount == 0 ? Visibility.Visible : Visibility.Collapsed;
                    }
                    return Visibility.Collapsed;

                default:
                    // Intensitaet zu Farbe
                    if (value is double intensity)
                    {
                        // Gradient von Blau (kalt) ueber Gruen zu Rot (heiss)
                        byte r, g, b;

                        if (intensity < 0.5)
                        {
                            // Blau zu Gruen
                            var t = intensity * 2;
                            r = 0;
                            g = (byte)(255 * t);
                            b = (byte)(255 * (1 - t));
                        }
                        else
                        {
                            // Gruen zu Rot
                            var t = (intensity - 0.5) * 2;
                            r = (byte)(255 * t);
                            g = (byte)(255 * (1 - t));
                            b = 0;
                        }

                        return Color.FromRgb(r, g, b);
                    }
                    return Colors.Blue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
