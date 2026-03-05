using System.Windows;
using System.Windows.Controls;
using Zenox.Wpf.Views;

namespace Zenox.Wpf.UI.Helpers
{
    /// <summary>
    /// Helper zum Initialisieren von UI-spezifischen Features für Views
    /// </summary>
    public static class ViewInitializationHelper
    {
        /// <summary>
        /// Initialisiert die EinstellungenView mit UI-spezifischen Features
        /// </summary>
        public static void InitializeEinstellungenView(EinstellungenView view)
        {
            if (view == null) return;

            // Subscribe to the UIInitializeRequested event
            view.UIInitializeRequested += (sender, e) =>
            {
                if (sender is EinstellungenView settingsView)
                {
                    // Find and wire up the shortcut manager button
                    var openButton = settingsView.FindName("OpenShortcutManagerButton") as Button;
                    if (openButton != null)
                    {
                        openButton.Click += (s, args) => OpenShortcutManager();
                    }
                }
            };
        }

        /// <summary>
        /// Öffnet den Shortcut-Manager in einem separaten Fenster
        /// </summary>
        private static void OpenShortcutManager()
        {
            var settingsWindow = new Window
            {
                Title = "Tastenkürzel verwalten",
                Width = 900,
                Height = 600,
                Content = new Views.ShortcutManagerView(),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = Application.Current.MainWindow
            };
            
            settingsWindow.ShowDialog();
        }
    }
}
