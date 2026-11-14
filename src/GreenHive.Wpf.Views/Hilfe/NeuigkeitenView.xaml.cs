using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GreenHive.Wpf.Views.Hilfe
{
	/// <summary>
	/// Interaktionslogik fuer NeuigkeitenView.xaml
	/// </summary>
	public partial class NeuigkeitenView : UserControl
	{
		public NeuigkeitenView()
		{
			InitializeComponent();
			// DataContext wird jetzt ueber XAML Resources gesetzt,
			// damit er nicht vom TabViewManager ueberschrieben wird
		}
	}

	/// <summary>
	/// Converter der null/empty Strings zu Collapsed konvertiert
	/// </summary>
	public class NullToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return Visibility.Collapsed;

			if (value is string str && string.IsNullOrWhiteSpace(str))
				return Visibility.Collapsed;

			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
