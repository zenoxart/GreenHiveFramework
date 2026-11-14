using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Views
{
    /// <summary>
    /// Interaktionslogik für EinstellungenView.xaml
    /// </summary>
    public partial class EinstellungenView : UserControl
    {
        /// <summary>
        /// Event that is fired when the view is loaded and UI-specific initialization can be performed
        /// </summary>
        public event EventHandler? UIInitializeRequested;

        public EinstellungenView()
        {
            InitializeComponent();
            Loaded += EinstellungenView_Loaded;
        }

        private void EinstellungenView_Loaded(object sender, RoutedEventArgs e)
        {
            // Select first item by default
            var settingsTree = FindName("SettingsTree") as TreeView;
            if (settingsTree?.Items.Count > 0 && settingsTree.Items[0] is TreeViewItem firstItem)
            {
                firstItem.IsSelected = true;
                if (firstItem.Items.Count > 0 && firstItem.Items[0] is TreeViewItem firstChild)
                {
                    firstChild.IsSelected = true;
                    ShowPanel("AutomationSettings");
                }
            }

            // Raise event for UI-specific initialization
            UIInitializeRequested?.Invoke(this, EventArgs.Empty);
            
            // Initialize Window Management
            InitializeWindowManagement();
            
            // Initialize Language Settings
            InitializeLanguageSettings();
        }

        private void SettingsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
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
            var automationPanel = FindName("AutomationSettingsPanel") as StackPanel;
            var categoryPanel = FindName("CategorySettingsPanel") as StackPanel;
            var managementPanel = FindName("ManagementSettingsPanel") as StackPanel;
            var loadSnapshotPanel = FindName("LoadSnapshotPanel") as StackPanel;
            var shortcutPanel = FindName("ShortcutSettingsPanel") as StackPanel;
            var windowManagementPanel = FindName("WindowManagementPanel") as StackPanel;
            var languagePanel = FindName("LanguageSettingsPanel") as StackPanel;

            // Hide all panels
            if (automationPanel != null) automationPanel.Visibility = Visibility.Collapsed;
            if (categoryPanel != null) categoryPanel.Visibility = Visibility.Collapsed;
            if (managementPanel != null) managementPanel.Visibility = Visibility.Collapsed;
            if (loadSnapshotPanel != null) loadSnapshotPanel.Visibility = Visibility.Collapsed;
            if (shortcutPanel != null) shortcutPanel.Visibility = Visibility.Collapsed;
            if (windowManagementPanel != null) windowManagementPanel.Visibility = Visibility.Collapsed;
            if (languagePanel != null) languagePanel.Visibility = Visibility.Collapsed;

            // Show selected panel
            switch (panelName)
            {
                case "AutomationSettings":
                    if (automationPanel != null) automationPanel.Visibility = Visibility.Visible;
                    break;
                case "CategorySettings":
                    if (categoryPanel != null) categoryPanel.Visibility = Visibility.Visible;
                    break;
                case "ManagementSettings":
                    if (managementPanel != null) managementPanel.Visibility = Visibility.Visible;
                    break;
                case "LoadSnapshot":
                    if (loadSnapshotPanel != null) loadSnapshotPanel.Visibility = Visibility.Visible;
                    break;
                case "ShortcutSettings":
                    if (shortcutPanel != null)
                    {
                        shortcutPanel.Visibility = Visibility.Visible;
                    }
                    break;
                case "WindowManagement":
                    if (windowManagementPanel != null)
                    {
                        windowManagementPanel.Visibility = Visibility.Visible;
                        LoadWindowManagementData();
                    }
                    break;
                case "LanguageSettings":
                    if (languagePanel != null)
                    {
                        languagePanel.Visibility = Visibility.Visible;
                        LoadLanguageSettings();
                    }
                    break;
            }
        }

        #region Window Management

        private void InitializeWindowManagement()
        {
            try
            {
                // Finde Controls
                var restoreToggle = FindName("RestoreOnStartupToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var autoSaveToggle = FindName("AutoSaveToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var multiInstanceToggle = FindName("SupportMultipleInstancesToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var maxCountTextBox = FindName("MaxWindowCountTextBox") as TextBox;
                var refreshButton = FindName("RefreshWindowListButton") as Button;
                var clearButton = FindName("ClearAllWindowsButton") as Button;
                var saveButton = FindName("SaveWindowSettingsButton") as Button;

                // Event-Handler registrieren
                if (refreshButton != null)
                    refreshButton.Click += RefreshWindowList_Click;
                
                if (clearButton != null)
                    clearButton.Click += ClearAllWindows_Click;
                
                if (saveButton != null)
                    saveButton.Click += SaveWindowSettings_Click;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Initialisieren der Window Management UI: {ex.Message}");
            }
        }

        private void LoadWindowManagementData()
        {
            try
            {
                var service = Helpers.WindowManagerServiceLocator.Instance;
                if (service == null)
                {
                    System.Diagnostics.Debug.WriteLine("WindowManagerService nicht verfügbar");
                    return;
                }

                var settings = service.GetGlobalSettings() as dynamic;
                if (settings == null) return;

                // Lade globale Einstellungen
                var restoreToggle = FindName("RestoreOnStartupToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var autoSaveToggle = FindName("AutoSaveToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var multiInstanceToggle = FindName("SupportMultipleInstancesToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var maxCountTextBox = FindName("MaxWindowCountTextBox") as TextBox;

                if (restoreToggle != null) restoreToggle.IsChecked = settings.RestoreOnStartup;
                if (autoSaveToggle != null) autoSaveToggle.IsChecked = settings.AutoSave;
                if (multiInstanceToggle != null) multiInstanceToggle.IsChecked = settings.SupportMultipleInstances;
                if (maxCountTextBox != null) maxCountTextBox.Text = settings.MaxWindowCount.ToString();

                // Lade Statistiken
                UpdateStatistics();

                // Lade Fensterliste
                LoadWindowList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Window Management Daten: {ex.Message}");
            }
        }

        private void LoadWindowList()
        {
            try
            {
                var service = Helpers.WindowManagerServiceLocator.Instance;
                if (service == null) return;

                var windows = service.GetAllWindowSettings() as System.Collections.IEnumerable;

                var listControl = FindName("WindowListControl") as ItemsControl;
                if (listControl != null && windows != null)
                {
                    listControl.ItemsSource = windows;

                    // Registriere Click-Handler für Delete-Buttons
                    listControl.ApplyTemplate();
                    listControl.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Fensterliste: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                var service = Helpers.WindowManagerServiceLocator.Instance;
                if (service == null) return;
                
                var savedCountText = FindName("SavedWindowsCountText") as TextBlock;
                var trackedCountText = FindName("TrackedWindowsCountText") as TextBlock;

                if (savedCountText != null)
                    savedCountText.Text = service.GetWindowCount().ToString();
                
                if (trackedCountText != null)
                    trackedCountText.Text = service.GetTrackedWindowCount().ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Aktualisieren der Statistiken: {ex.Message}");
            }
        }

        private void RefreshWindowList_Click(object sender, RoutedEventArgs e)
        {
            LoadWindowManagementData();
        }

        private void ClearAllWindows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Moechten Sie wirklich alle gespeicherten Fensterpositionen loeschen?",
                    "Bestaetigung",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var service = Helpers.WindowManagerServiceLocator.Instance;
                    if (service != null)
                    {
                        service.ClearAllSettings();
                        LoadWindowManagementData();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveWindowSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = Helpers.WindowManagerServiceLocator.Instance;
                if (service == null)
                {
                    MessageBox.Show("WindowManagerService nicht verfügbar", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Lese Werte aus UI
                var restoreToggle = FindName("RestoreOnStartupToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var autoSaveToggle = FindName("AutoSaveToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var multiInstanceToggle = FindName("SupportMultipleInstancesToggle") as System.Windows.Controls.Primitives.ToggleButton;
                var maxCountTextBox = FindName("MaxWindowCountTextBox") as TextBox;

                var restoreOnStartup = restoreToggle?.IsChecked ?? true;
                var autoSave = autoSaveToggle?.IsChecked ?? true;
                var supportMultipleInstances = multiInstanceToggle?.IsChecked ?? true;
                var maxCount = int.TryParse(maxCountTextBox?.Text, out var count) ? count : 10;

                // Speichere Einstellungen
                service.UpdateGlobalSettings(restoreOnStartup, maxCount, autoSave, supportMultipleInstances);
                
                MessageBox.Show("Einstellungen erfolgreich gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Language Settings

        private void InitializeLanguageSettings()
        {
            try
            {
                var germanButton = FindName("SelectGermanButton") as Button;
                var englishButton = FindName("SelectEnglishButton") as Button;

                if (germanButton != null)
                    germanButton.Click += SelectGerman_Click;

                if (englishButton != null)
                    englishButton.Click += SelectEnglish_Click;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Initialisieren der Language Settings: {ex.Message}");
            }
        }

        private void LoadLanguageSettings()
        {
            try
            {
                var service = Helpers.LanguageServiceLocator.Instance;
                if (service == null)
                {
                    System.Diagnostics.Debug.WriteLine("LanguageService nicht verfügbar");
                    return;
                }

                var currentLanguage = service.GetCurrentLanguage();

                // Update UI indicators
                var germanIndicator = FindName("GermanSelectedIndicator") as TextBlock;
                var englishIndicator = FindName("EnglishSelectedIndicator") as TextBlock;
                var currentLanguageText = FindName("CurrentLanguageText") as TextBlock;

                if (germanIndicator != null && englishIndicator != null)
                {
                    if (currentLanguage == "de-DE")
                    {
                        germanIndicator.Visibility = Visibility.Visible;
                        englishIndicator.Visibility = Visibility.Collapsed;
                        if (currentLanguageText != null)
                            currentLanguageText.Text = "Deutsch (Deutschland)";
                    }
                    else
                    {
                        germanIndicator.Visibility = Visibility.Collapsed;
                        englishIndicator.Visibility = Visibility.Visible;
                        if (currentLanguageText != null)
                            currentLanguageText.Text = "English (United States)";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Language Settings: {ex.Message}");
            }
        }

        private void SelectGerman_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = Helpers.LanguageServiceLocator.Instance;
                if (service != null)
                {
                    service.ChangeLanguage("de-DE");
                    LoadLanguageSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Wechseln der Sprache: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectEnglish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = Helpers.LanguageServiceLocator.Instance;
                if (service != null)
                {
                    service.ChangeLanguage("en-US");
                    LoadLanguageSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error changing language: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}








