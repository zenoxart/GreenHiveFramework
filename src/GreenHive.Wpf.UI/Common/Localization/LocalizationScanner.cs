using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace GreenHive.Wpf.UI.Common.Localization
{
    /// <summary>
    /// Scannt die Anwendung nach nicht-lokalisierten Strings
    /// </summary>
    public static class LocalizationScanner
    {
        /// <summary>
        /// Findet hardcodierte Strings in XAML-Dateien
        /// </summary>
        public static List<HardcodedString> ScanXamlFiles(string basePath)
        {
            var results = new List<HardcodedString>();
            var xamlFiles = Directory.GetFiles(basePath, "*.xaml", SearchOption.AllDirectories);

            // Patterns für hardcodierte Strings
            var patterns = new[]
            {
                @"Text=""([^""]+)""",
                @"Content=""([^""]+)""",
                @"Header=""([^""]+)""",
                @"Title=""([^""]+)""",
                @"ToolTip=""([^""]+)""",
                @"Watermark=""([^""]+)""",
                @"Placeholder=""([^""]+)""",
            };

            foreach (var file in xamlFiles)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var lines = File.ReadAllLines(file);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        
                        // Überspringe bereits lokalisierte Zeilen
                        if (line.Contains("StaticResource") || line.Contains("DynamicResource"))
                            continue;

                        foreach (var pattern in patterns)
                        {
                            var matches = Regex.Matches(line, pattern);
                            foreach (Match match in matches)
                            {
                                var text = match.Groups[1].Value;
                                
                                // Filtere technische Werte aus
                                if (IsLikelyUserFacingText(text))
                                {
                                    results.Add(new HardcodedString
                                    {
                                        FilePath = file,
                                        LineNumber = i + 1,
                                        Text = text,
                                        Context = line.Trim(),
                                        Type = "XAML"
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fehler beim Scannen von {file}: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Findet hardcodierte Strings in C#-Dateien
        /// </summary>
        public static List<HardcodedString> ScanCSharpFiles(string basePath)
        {
            var results = new List<HardcodedString>();
            var csFiles = Directory.GetFiles(basePath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToArray();

            var patterns = new[]
            {
                @"MessageBox\.Show\(""([^""]+)""",
                @"ShowSuccess\(""([^""]+)""",
                @"ShowError\(""([^""]+)""",
                @"ShowInfo\(""([^""]+)""",
                @"ShowWarning\(""([^""]+)""",
                @"Title\s*=\s*""([^""]+)""",
                @"Name\s*=\s*""([^""]+)""",
            };

            foreach (var file in csFiles)
            {
                try
                {
                    var lines = File.ReadAllLines(file);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        
                        // Überspringe Kommentare
                        if (line.TrimStart().StartsWith("//"))
                            continue;

                        foreach (var pattern in patterns)
                        {
                            var matches = Regex.Matches(line, pattern);
                            foreach (Match match in matches)
                            {
                                var text = match.Groups[1].Value;
                                
                                if (IsLikelyUserFacingText(text))
                                {
                                    results.Add(new HardcodedString
                                    {
                                        FilePath = file,
                                        LineNumber = i + 1,
                                        Text = text,
                                        Context = line.Trim(),
                                        Type = "C#"
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fehler beim Scannen von {file}: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Prüft ob ein String wahrscheinlich ein benutzerfreundlicher Text ist
        /// </summary>
        private static bool IsLikelyUserFacingText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Filtere technische Werte aus
            if (text.Length < 2)
                return false;

            // Filtere Zahlen, Pfade, URLs etc.
            if (Regex.IsMatch(text, @"^[\d\.\-\+]+$")) // Nur Zahlen
                return false;

            if (text.Contains("://") || text.Contains("\\") || text.Contains("/"))
                return false;

            // Filtere Binding-Pfade
            if (text.Contains(".") && text.Split('.').Length > 2)
                return false;

            // Filtere sehr kurze technische Strings
            var technicalPatterns = new[] { "OK", "ID", "URL", "HTTP", "HTTPS", "FTP", "TCP", "UDP", "IP" };
            if (technicalPatterns.Contains(text.ToUpper()) && text.Length < 4)
                return false;

            return true;
        }

        /// <summary>
        /// Generiert einen Report als Markdown
        /// </summary>
        public static string GenerateReport(List<HardcodedString> xamlStrings, List<HardcodedString> csharpStrings)
        {
            var report = "# Lokalisierungs-Scan Report\n\n";
            report += $"**Datum:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
            report += $"**Gefundene hardcodierte Strings:**\n";
            report += $"- XAML: {xamlStrings.Count}\n";
            report += $"- C#: {csharpStrings.Count}\n";
            report += $"- **Gesamt: {xamlStrings.Count + csharpStrings.Count}**\n\n";

            // XAML Strings
            if (xamlStrings.Any())
            {
                report += "## XAML-Dateien\n\n";
                
                var byFile = xamlStrings.GroupBy(s => s.FilePath);
                foreach (var group in byFile.OrderBy(g => g.Key))
                {
                    report += $"### {Path.GetFileName(group.Key)}\n";
                    report += $"*Pfad: {group.Key}*\n\n";
                    
                    foreach (var item in group.OrderBy(i => i.LineNumber))
                    {
                        report += $"**Zeile {item.LineNumber}:** `{item.Text}`\n";
                        report += $"```xml\n{item.Context}\n```\n\n";
                    }
                }
            }

            // C# Strings
            if (csharpStrings.Any())
            {
                report += "## C#-Dateien\n\n";
                
                var byFile = csharpStrings.GroupBy(s => s.FilePath);
                foreach (var group in byFile.OrderBy(g => g.Key))
                {
                    report += $"### {Path.GetFileName(group.Key)}\n";
                    report += $"*Pfad: {group.Key}*\n\n";
                    
                    foreach (var item in group.OrderBy(i => i.LineNumber))
                    {
                        report += $"**Zeile {item.LineNumber}:** `{item.Text}`\n";
                        report += $"```csharp\n{item.Context}\n```\n\n";
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Zeigt einen Scan-Dialog
        /// </summary>
        public static void ShowScanDialog()
        {
            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var projectPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));

                System.Diagnostics.Debug.WriteLine($"Scanne Projekt: {projectPath}");

                var xamlStrings = ScanXamlFiles(projectPath);
                var csharpStrings = ScanCSharpFiles(projectPath);

                var report = GenerateReport(xamlStrings, csharpStrings);
                
                var reportPath = Path.Combine(projectPath, "Docs", $"Localization-Scan-{DateTime.Now:yyyyMMdd-HHmmss}.md");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, report);

                MessageBox.Show(
                    $"Scan abgeschlossen!\n\n" +
                    $"XAML: {xamlStrings.Count} Strings\n" +
                    $"C#: {csharpStrings.Count} Strings\n" +
                    $"Gesamt: {xamlStrings.Count + csharpStrings.Count}\n\n" +
                    $"Report gespeichert:\n{reportPath}",
                    "Lokalisierungs-Scan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Öffne Report
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = reportPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Scan: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Repräsentiert einen hardcodierten String
    /// </summary>
    public class HardcodedString
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string Text { get; set; }
        public string Context { get; set; }
        public string Type { get; set; } // "XAML" oder "C#"
    }
}
