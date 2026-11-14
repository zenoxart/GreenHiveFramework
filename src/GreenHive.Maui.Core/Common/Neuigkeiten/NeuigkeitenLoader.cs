using System.Diagnostics;
using System.Text.Json;

namespace GreenHive.NET.Core.Common.Neuigkeiten
{
    /// <summary>
    /// Plattformübergreifender Loader für Neuigkeiten-Konfigurationen
    /// Unterstützt JSON-Format für maximale Kompatibilität
    /// </summary>
    public static class NeuigkeitenLoader
    {
        /// <summary>
        /// Lädt die Neuigkeiten-Konfiguration aus einer JSON-Datei
        /// </summary>
        /// <param name="jsonDateiPfad">Pfad zur JSON-Konfigurationsdatei</param>
        /// <returns>Die geladene Konfiguration oder eine leere Konfiguration bei Fehler</returns>
        public static NeuigkeitenKonfiguration LadeAusDatei(string jsonDateiPfad)
        {
            try
            {
                if (!File.Exists(jsonDateiPfad))
                {
                    Debug.WriteLine($"[NeuigkeitenLoader] Konfigurationsdatei nicht gefunden: {jsonDateiPfad}");
                    return ErstelleStandardKonfiguration();
                }

                Debug.WriteLine($"[NeuigkeitenLoader] Lade Konfiguration aus: {jsonDateiPfad}");

                var jsonContent = File.ReadAllText(jsonDateiPfad);
                var konfiguration = JsonSerializer.Deserialize<NeuigkeitenKonfiguration>(
                    jsonContent, 
                    new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    });

                if (konfiguration != null)
                {
                    Debug.WriteLine($"[NeuigkeitenLoader] Erfolgreich geladen: {konfiguration.Versionen.Count} Version(en)");
                    foreach (var version in konfiguration.Versionen)
                    {
                        Debug.WriteLine($"[NeuigkeitenLoader]   Version {version.Version}: {version.Kategorien.Count} Kategorie(n)");
                    }
                }

                return konfiguration ?? ErstelleStandardKonfiguration();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NeuigkeitenLoader] FEHLER beim Laden: {ex.Message}");
                Debug.WriteLine($"[NeuigkeitenLoader] StackTrace: {ex.StackTrace}");
                return ErstelleStandardKonfiguration();
            }
        }

        /// <summary>
        /// Lädt die Neuigkeiten-Konfiguration aus einem Stream (für eingebettete Ressourcen)
        /// </summary>
        /// <param name="stream">Stream mit JSON-Daten</param>
        /// <returns>Die geladene Konfiguration oder eine leere Konfiguration bei Fehler</returns>
        public static async Task<NeuigkeitenKonfiguration> LadeAusStreamAsync(Stream stream)
        {
            try
            {
                var konfiguration = await JsonSerializer.DeserializeAsync<NeuigkeitenKonfiguration>(
                    stream,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return konfiguration ?? ErstelleStandardKonfiguration();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NeuigkeitenLoader] FEHLER beim Laden aus Stream: {ex.Message}");
                return ErstelleStandardKonfiguration();
            }
        }

        /// <summary>
        /// Lädt die Neuigkeiten-Konfiguration aus JSON-String
        /// </summary>
        /// <param name="jsonString">JSON-String mit Konfiguration</param>
        /// <returns>Die geladene Konfiguration oder eine leere Konfiguration bei Fehler</returns>
        public static NeuigkeitenKonfiguration LadeAusJson(string jsonString)
        {
            try
            {
                var konfiguration = JsonSerializer.Deserialize<NeuigkeitenKonfiguration>(
                    jsonString,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return konfiguration ?? ErstelleStandardKonfiguration();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NeuigkeitenLoader] FEHLER beim Laden aus JSON: {ex.Message}");
                return ErstelleStandardKonfiguration();
            }
        }

        /// <summary>
        /// Lädt die Neuigkeiten-Konfiguration aus dem Standardpfad
        /// Sucht in verschiedenen Standardlocations je nach Plattform
        /// </summary>
        /// <returns>Die geladene Konfiguration</returns>
        public static NeuigkeitenKonfiguration LadeStandard()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var configPaths = new[]
            {
                Path.Combine(basePath, "Configuration", "NeuigkeitenKonfiguration.json"),
                Path.Combine(basePath, "Assets", "Configuration", "NeuigkeitenKonfiguration.json"),
                Path.Combine(basePath, "Resources", "Configuration", "NeuigkeitenKonfiguration.json")
            };

            Debug.WriteLine($"[NeuigkeitenLoader] LadeStandard() - BasePath: {basePath}");

            foreach (var configPath in configPaths)
            {
                Debug.WriteLine($"[NeuigkeitenLoader] Prüfe Pfad: {configPath}");
                if (File.Exists(configPath))
                {
                    Debug.WriteLine($"[NeuigkeitenLoader] Datei gefunden: {configPath}");
                    return LadeAusDatei(configPath);
                }
            }

            Debug.WriteLine("[NeuigkeitenLoader] Keine Konfigurationsdatei gefunden, verwende Standard");
            return ErstelleStandardKonfiguration();
        }

        /// <summary>
        /// Erstellt eine Standard-Konfiguration als Fallback
        /// </summary>
        public static NeuigkeitenKonfiguration ErstelleStandardKonfiguration()
        {
            Debug.WriteLine("[NeuigkeitenLoader] Verwende Standard-Konfiguration (Fallback)");

            return new NeuigkeitenKonfiguration
            {
                Titel = "Neuigkeiten",
                Untertitel = "Was gibt es Neues?",
                Versionen = new()
                {
                    new VersionNeuigkeiten
                    {
                        Version = "1.0.0",
                        Datum = DateTime.Now,
                        ReleaseTyp = "Major",
                        Kategorien = new()
                        {
                            new NeuigkeitenKategorie
                            {
                                Titel = "? Neue Features",
                                Icon = "?",
                                Farbe = "#007ACC",
                                Eintraege = new()
                                {
                                    new NeuigkeitEintrag
                                    {
                                        Text = "RelayMenu System",
                                        Beschreibung = "Hierarchische Menüstrukturen mit Commands",
                                        Icon = "??"
                                    },
                                    new NeuigkeitEintrag
                                    {
                                        Text = "TabbedPage Navigation",
                                        Beschreibung = "Moderne Tab-basierte Navigation",
                                        Icon = "??"
                                    },
                                    new NeuigkeitEintrag
                                    {
                                        Text = "AppKontext System",
                                        Beschreibung = "Zentrale Verwaltung von Services",
                                        Icon = "??"
                                    }
                                }
                            },
                            new NeuigkeitenKategorie
                            {
                                Titel = "?? Bug Fixes",
                                Icon = "??",
                                Farbe = "#E74C3C",
                                Eintraege = new()
                                {
                                    new NeuigkeitEintrag
                                    {
                                        Text = "Exception Handling verbessert",
                                        Beschreibung = "Globale Exception-Handler implementiert",
                                        Icon = "???"
                                    }
                                }
                            },
                            new NeuigkeitenKategorie
                            {
                                Titel = "?? Dokumentation",
                                Icon = "??",
                                Farbe = "#2ECC71",
                                Eintraege = new()
                                {
                                    new NeuigkeitEintrag
                                    {
                                        Text = "Vollständige Code-Dokumentation",
                                        Beschreibung = "XML-Kommentare für alle öffentlichen APIs",
                                        Icon = "??"
                                    },
                                    new NeuigkeitEintrag
                                    {
                                        Text = "Implementierungs-Leitfäden",
                                        Beschreibung = "Detaillierte Markdown-Dokumentation",
                                        Icon = "??"
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Speichert eine Konfiguration als JSON-Datei
        /// </summary>
        /// <param name="konfiguration">Die zu speichernde Konfiguration</param>
        /// <param name="jsonDateiPfad">Zielpfad für die JSON-Datei</param>
        public static void SpeichereAlsDatei(NeuigkeitenKonfiguration konfiguration, string jsonDateiPfad)
        {
            try
            {
                var directory = Path.GetDirectoryName(jsonDateiPfad);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var jsonString = JsonSerializer.Serialize(konfiguration, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.WriteAllText(jsonDateiPfad, jsonString);
                Debug.WriteLine($"[NeuigkeitenLoader] Konfiguration gespeichert: {jsonDateiPfad}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NeuigkeitenLoader] FEHLER beim Speichern: {ex.Message}");
            }
        }
    }
}
