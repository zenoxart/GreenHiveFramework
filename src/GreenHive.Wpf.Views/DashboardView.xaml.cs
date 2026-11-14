using System.Windows.Controls;

using System.Windows;
using System.Windows.Media.Animation;

namespace GreenHive.Wpf.Views
{
	/// <summary>
	/// Interaktionslogik für DashboardView.xaml
	/// </summary>
	public partial class DashboardView : UserControl
	{
		public DashboardView()
		{
			InitializeComponent();
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			// Storyboard aus Ressourcen holen
			var storyboard = (Storyboard)FindResource("QuickActionsStoryboard");
			// Animation zurücksetzen, indem sie gestoppt und neu gestartet wird
			storyboard.Stop(QuickActionsBorder);
			storyboard.Begin(QuickActionsBorder, true);
		}
	}
}
