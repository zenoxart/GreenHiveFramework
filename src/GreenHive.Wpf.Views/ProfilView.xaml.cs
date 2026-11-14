using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Views
{
    /// <summary>
    /// Interaktionslogik für ProfilView.xaml
    /// </summary>
    public partial class ProfilView : UserControl
    {
        public ProfilView()
        {
            InitializeComponent();
            Loaded += ProfilView_Loaded;
        }

        private void ProfilView_Loaded(object sender, RoutedEventArgs e)
        {
            // Select first item by default
            var profileTree = FindName("ProfileTree") as TreeView;
            if (profileTree?.Items.Count > 0 && profileTree.Items[0] is TreeViewItem firstItem)
            {
                firstItem.IsSelected = true;
                ShowPanel("ProfileOverview");
            }
        }

        private void ProfileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem)
            {
                var tag = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(tag))
                {
                    ShowPanel(tag);
                }
            }
        }

        private void ShowPanel(string panelName)
        {
            // Find all panels
            var overviewPanel = FindName("ProfileOverviewPanel") as StackPanel;
            var personalInfoPanel = FindName("PersonalInfoPanel") as StackPanel;
            var notificationsPanel = FindName("NotificationsPanel") as StackPanel;
            var privacyPanel = FindName("PrivacyPanel") as StackPanel;
            var securityPanel = FindName("SecurityPanel") as StackPanel;

            // Hide all panels
            if (overviewPanel != null) overviewPanel.Visibility = Visibility.Collapsed;
            if (personalInfoPanel != null) personalInfoPanel.Visibility = Visibility.Collapsed;
            if (notificationsPanel != null) notificationsPanel.Visibility = Visibility.Collapsed;
            if (privacyPanel != null) privacyPanel.Visibility = Visibility.Collapsed;
            if (securityPanel != null) securityPanel.Visibility = Visibility.Collapsed;

            // Show selected panel
            switch (panelName)
            {
                case "ProfileOverview":
                    if (overviewPanel != null) overviewPanel.Visibility = Visibility.Visible;
                    break;
                case "PersonalInfo":
                    if (personalInfoPanel != null) personalInfoPanel.Visibility = Visibility.Visible;
                    break;
                case "Notifications":
                    if (notificationsPanel != null) notificationsPanel.Visibility = Visibility.Visible;
                    break;
                case "Privacy":
                    if (privacyPanel != null) privacyPanel.Visibility = Visibility.Visible;
                    break;
                case "Security":
                    if (securityPanel != null) securityPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
    }
}

