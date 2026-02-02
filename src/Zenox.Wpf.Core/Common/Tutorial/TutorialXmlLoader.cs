using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Zenox.Wpf.Core.Common.Tutorial
{
    /// <summary>
    /// Lädt Tutorial-Konfigurationen aus XML-Dateien
    /// </summary>
    public static class TutorialXmlLoader
    {
        /// <summary>
        /// Lädt Tutorials aus einer XML-Datei
        /// </summary>
        /// <param name="xmlFilePath">Pfad zur XML-Datei</param>
        /// <returns>Dictionary mit Tutorial-Schlüssel und zugehörigen Schritten</returns>
        public static Dictionary<string, List<TutorialStep>> LoadTutorialsFromXml(string xmlFilePath)
        {
            var tutorials = new Dictionary<string, List<TutorialStep>>();

            if (!File.Exists(xmlFilePath))
            {
                throw new FileNotFoundException($"Tutorial-XML-Datei nicht gefunden: {xmlFilePath}");
            }

            try
            {
                var doc = XDocument.Load(xmlFilePath);
                var tutorialsElement = doc.Root;

                if (tutorialsElement?.Name != "Tutorials")
                {
                    throw new InvalidOperationException("XML-Datei muss ein 'Tutorials' Root-Element haben.");
                }

                foreach (var tutorialElement in tutorialsElement.Elements("Tutorial"))
                {
                    var tutorialKey = tutorialElement.Attribute("Key")?.Value;
                    if (string.IsNullOrEmpty(tutorialKey))
                    {
                        continue; // Tutorial ohne Key überspringen
                    }

                    var steps = new List<TutorialStep>();

                    foreach (var stepElement in tutorialElement.Elements("Step"))
                    {
                        var step = ParseTutorialStep(stepElement);
                        if (step != null)
                        {
                            steps.Add(step);
                        }
                    }

                    if (steps.Count > 0)
                    {
                        tutorials[tutorialKey] = steps;
                    }
                }
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException($"Fehler beim Parsen der XML-Datei: {ex.Message}", ex);
            }

            return tutorials;
        }

        /// <summary>
        /// Parsed aus den XML die Tutorial-Steps
        /// </summary>
        private static TutorialStep? ParseTutorialStep(XElement stepElement)
        {
            var key = stepElement.Attribute("Key")?.Value;
            var title = stepElement.Attribute("Title")?.Value;

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(title))
            {
                return null; // Unvollständiger Schritt
            }

            var step = new TutorialStep
            {
                Key = key,
                Title = title,
                InstructionText = stepElement.Element("InstructionText")?.Value ?? "",
                Position = ParseTutorialPosition(stepElement.Attribute("Position")?.Value),
                CanSkip = ParseBoolAttribute(stepElement.Attribute("CanSkip")?.Value, true),
                StepType = ParseTutorialStepType(stepElement.Attribute("StepType")?.Value),
                DelayMs = ParseIntAttribute(stepElement.Attribute("DelayMs")?.Value, 0)
            };

            return step;
        }

        /// <summary>
        /// Parsed aus dem XML die Tutorial-Popup-Position
        /// </summary>
        private static TutorialPosition ParseTutorialPosition(string? positionValue)
        {
            if (string.IsNullOrEmpty(positionValue))
                return TutorialPosition.Auto;

            return positionValue.ToLowerInvariant() switch
            {
                "top" => TutorialPosition.Top,
                "bottom" => TutorialPosition.Bottom,
                "left" => TutorialPosition.Left,
                "right" => TutorialPosition.Right,
                "center" => TutorialPosition.Center,
                _ => TutorialPosition.Auto
            };
        }

        /// <summary>
        /// Parsed aus dem XML die Tutorial-Display-Art
        /// </summary>
        private static TutorialStepType ParseTutorialStepType(string? stepTypeValue)
        {
            if (string.IsNullOrEmpty(stepTypeValue))
                return TutorialStepType.Highlight;

            return stepTypeValue.ToLowerInvariant() switch
            {
                "highlight" => TutorialStepType.Highlight,
                "tooltip" => TutorialStepType.Tooltip,
                "modal" => TutorialStepType.Modal,
                "pulse" => TutorialStepType.Pulse,
                _ => TutorialStepType.Highlight
            };
        }

        /// <summary>
        /// Parsed Wahrheitswerte
        /// </summary>
        private static bool ParseBoolAttribute(string? value, bool defaultValue)
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Parsed Integerwerte
        /// </summary>
        private static int ParseIntAttribute(string? value, int defaultValue)
        {
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Lädt Tutorials aus einem XML-String
        /// </summary>
        /// <param name="xmlContent">XML-Inhalt als String</param>
        /// <returns>Dictionary mit Tutorial-Schlüssel und zugehörigen Schritten</returns>
        public static Dictionary<string, List<TutorialStep>> LoadTutorialsFromXmlString(string xmlContent)
        {
            var tutorials = new Dictionary<string, List<TutorialStep>>();

            if (string.IsNullOrEmpty(xmlContent))
            {
                return tutorials;
            }

            try
            {
                var doc = XDocument.Parse(xmlContent);
                var tutorialsElement = doc.Root;

                if (tutorialsElement?.Name != "Tutorials")
                {
                    throw new InvalidOperationException("XML-Inhalt muss ein 'Tutorials' Root-Element haben.");
                }

                foreach (var tutorialElement in tutorialsElement.Elements("Tutorial"))
                {
                    var tutorialKey = tutorialElement.Attribute("Key")?.Value;
                    if (string.IsNullOrEmpty(tutorialKey))
                    {
                        continue;
                    }

                    var steps = new List<TutorialStep>();

                    foreach (var stepElement in tutorialElement.Elements("Step"))
                    {
                        var step = ParseTutorialStep(stepElement);
                        if (step != null)
                        {
                            steps.Add(step);
                        }
                    }

                    if (steps.Count > 0)
                    {
                        tutorials[tutorialKey] = steps;
                    }
                }
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException($"Fehler beim Parsen des XML-Inhalts: {ex.Message}", ex);
            }

            return tutorials;
        }
    }
}