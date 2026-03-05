using System;
using System.Windows;

namespace Zenox.Wpf.UI.Common.Localization
{
    /// <summary>
    /// Diagnostik-Tool für Lokalisierungsprobleme
    /// </summary>
    public static class LocalizationDiagnostics
    {
        /// <summary>
        /// Führt eine vollständige Diagnose der Lokalisierung durch
        /// </summary>
        public static void RunDiagnostics()
        {
            System.Diagnostics.Debug.WriteLine("=== LOKALISIERUNGS-DIAGNOSTIK START ===");

            try
            {
                // 1. Prüfe Application.Current
                if (Application.Current == null)
                {
                    System.Diagnostics.Debug.WriteLine("? Application.Current ist NULL!");
                    return;
                }
                System.Diagnostics.Debug.WriteLine("? Application.Current vorhanden");

                // 2. Prüfe ResourceDictionaries
                System.Diagnostics.Debug.WriteLine($"? MergedDictionaries Count: {Application.Current.Resources.MergedDictionaries.Count}");

                // 3. Liste alle MergedDictionaries auf
                int index = 0;
                foreach (var dict in Application.Current.Resources.MergedDictionaries)
                {
                    if (dict.Source != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  [{index}] Source: {dict.Source.OriginalString}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  [{index}] No Source (Count: {dict.Count} keys)");
                    }
                    index++;
                }

                // 4. Versuche Ressourcen zu laden
                try
                {
                    var deUri = new Uri("/Zenox.Wpf.UI;component/Resources/Localization/de-DE.xaml", UriKind.Relative);
                    var deDict = new ResourceDictionary { Source = deUri };
                    System.Diagnostics.Debug.WriteLine($"? de-DE.xaml geladen ({deDict.Count} keys)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? de-DE.xaml FEHLER: {ex.Message}");
                }

                try
                {
                    var enUri = new Uri("/Zenox.Wpf.UI;component/Resources/Localization/en-US.xaml", UriKind.Relative);
                    var enDict = new ResourceDictionary { Source = enUri };
                    System.Diagnostics.Debug.WriteLine($"? en-US.xaml geladen ({enDict.Count} keys)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? en-US.xaml FEHLER: {ex.Message}");
                }

                // 5. Teste Schlüsselzugriff
                try
                {
                    var testKey = Application.Current.FindResource("Common_Save");
                    System.Diagnostics.Debug.WriteLine($"? Testzugriff 'Common_Save': {testKey}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? Testzugriff 'Common_Save' FEHLER: {ex.Message}");
                }

                // 6. Prüfe LocalizationManager
                try
                {
                    var manager = LocalizationManager.Instance;
                    var currentLang = manager.GetCurrentLanguage();
                    System.Diagnostics.Debug.WriteLine($"? LocalizationManager aktuelle Sprache: {currentLang}");
                    
                    var availableLangs = manager.GetAvailableLanguages();
                    System.Diagnostics.Debug.WriteLine($"? Verfügbare Sprachen: {availableLangs.Count}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? LocalizationManager FEHLER: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? KRITISCHER DIAGNOSTIK-FEHLER: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            System.Diagnostics.Debug.WriteLine("=== LOKALISIERUNGS-DIAGNOSTIK ENDE ===");
        }

        /// <summary>
        /// Zeigt eine Diagnose-Messagebox
        /// </summary>
        public static void ShowDiagnosticsDialog()
        {
            var report = GenerateDiagnosticReport();
            MessageBox.Show(report, "Lokalisierungs-Diagnostik", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Generiert einen Diagnostik-Report
        /// </summary>
        public static string GenerateDiagnosticReport()
        {
            var report = "=== LOKALISIERUNGS-DIAGNOSTIK ===\n\n";

            try
            {
                var manager = LocalizationManager.Instance;
                report += $"Aktuelle Sprache: {manager.GetCurrentLanguage()}\n";
                report += $"Verfügbare Sprachen: {manager.GetAvailableLanguages().Count}\n\n";

                if (Application.Current != null)
                {
                    report += $"MergedDictionaries: {Application.Current.Resources.MergedDictionaries.Count}\n";
                    
                    // Zähle Lokalisierungs-Dictionaries
                    int locCount = 0;
                    foreach (var dict in Application.Current.Resources.MergedDictionaries)
                    {
                        if (dict.Source != null && dict.Source.OriginalString.Contains("/Localization/"))
                        {
                            locCount++;
                        }
                    }
                    report += $"Lokalisierungs-Dictionaries: {locCount}\n";
                }

                report += "\nStatus: OK ?";
            }
            catch (Exception ex)
            {
                report += $"\nFEHLER: {ex.Message}\n";
                report += $"\nStatus: FEHLER ?";
            }

            return report;
        }
    }
}
