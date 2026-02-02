using System;
using System.Collections.Generic;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Repräsentiert das Ergebnis einer kontextbewussten ML-Validierung.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// ID der ursprünglichen Validierungsanfrage.
        /// </summary>
        public Guid RequestId { get; init; }

        /// <summary>
        /// Zeitstempel der Validierung.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.Now;

        /// <summary>
        /// Der ursprüngliche Validierungskontext.
        /// </summary>
        public ValidationContext? Context { get; init; }

        /// <summary>
        /// Ermittelter Validierungszustand.
        /// </summary>
        public ValidationState State { get; set; } = ValidationState.Idle;

        /// <summary>
        /// Konfidenz der Validierung (0.0 - 1.0).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Abweichungs-Score (0 = keine Abweichung, höher = stärkere Abweichung).
        /// </summary>
        public double DeviationScore { get; set; }

        /// <summary>
        /// Erwarteter Wert basierend auf dem Kontext (falls berechenbar).
        /// </summary>
        public double? ExpectedValue { get; set; }

        /// <summary>
        /// Tatsächlicher Wert als double.
        /// </summary>
        public double? ActualValue { get; set; }

        /// <summary>
        /// Prozentuale Abweichung vom Erwartungswert.
        /// </summary>
        public double? DeviationPercentage { get; set; }

        /// <summary>
        /// Benutzerfreundliche Zusammenfassung des Ergebnisses.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Detaillierte Erklärung der Validierung.
        /// </summary>
        public string? Explanation { get; set; }

        /// <summary>
        /// Empfehlungen für den Benutzer.
        /// </summary>
        public List<string> Recommendations { get; set; } = new();

        /// <summary>
        /// Ähnliche historische Werte (falls gefunden).
        /// </summary>
        public List<SimilarValueInfo> SimilarValues { get; set; } = new();

        /// <summary>
        /// Fehlermeldung (falls State == Error).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Dauer der Validierung in Millisekunden.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Gibt an, ob die Validierung erfolgreich abgeschlossen wurde.
        /// </summary>
        public bool IsSuccess => State != ValidationState.Error && State != ValidationState.Idle;

        /// <summary>
        /// Gibt an, ob der Wert als problematisch eingestuft wurde.
        /// </summary>
        public bool HasIssue => State == ValidationState.Warning || State == ValidationState.Anomaly;

        /// <summary>
        /// Erstellt ein erfolgreiches Validierungsergebnis (Valid).
        /// </summary>
        public static ValidationResult Success(ValidationContext context, double confidence = 1.0, string? summary = null)
        {
            return new ValidationResult
            {
                RequestId = context.RequestId,
                Context = context,
                State = ValidationState.Valid,
                Confidence = confidence,
                Summary = summary ?? "Der Wert ist im Kontext plausibel.",
                ActualValue = context.AsDouble()
            };
        }

        /// <summary>
        /// Erstellt ein Warnungs-Ergebnis.
        /// </summary>
        public static ValidationResult AsWarning(ValidationContext context, double deviationScore, 
            double? expectedValue = null, string? explanation = null)
        {
            var actual = context.AsDouble();
            double? deviationPct = null;
            if (actual.HasValue && expectedValue.HasValue && expectedValue.Value != 0)
            {
                deviationPct = Math.Abs((actual.Value - expectedValue.Value) / expectedValue.Value) * 100;
            }

            return new ValidationResult
            {
                RequestId = context.RequestId,
                Context = context,
                State = ValidationState.Warning,
                DeviationScore = deviationScore,
                ExpectedValue = expectedValue,
                ActualValue = actual,
                DeviationPercentage = deviationPct,
                Summary = "Der Wert weicht leicht vom Erwartungswert ab.",
                Explanation = explanation
            };
        }

        /// <summary>
        /// Erstellt ein Anomalie-Ergebnis.
        /// </summary>
        public static ValidationResult AsAnomaly(ValidationContext context, double deviationScore,
            double? expectedValue = null, string? explanation = null)
        {
            var actual = context.AsDouble();
            double? deviationPct = null;
            if (actual.HasValue && expectedValue.HasValue && expectedValue.Value != 0)
            {
                deviationPct = Math.Abs((actual.Value - expectedValue.Value) / expectedValue.Value) * 100;
            }

            return new ValidationResult
            {
                RequestId = context.RequestId,
                Context = context,
                State = ValidationState.Anomaly,
                DeviationScore = deviationScore,
                ExpectedValue = expectedValue,
                ActualValue = actual,
                DeviationPercentage = deviationPct,
                Summary = "Der Wert weicht stark vom Erwartungswert ab!",
                Explanation = explanation
            };
        }

        /// <summary>
        /// Erstellt ein Fehler-Ergebnis.
        /// </summary>
        public static ValidationResult Error(ValidationContext context, string errorMessage)
        {
            return new ValidationResult
            {
                RequestId = context.RequestId,
                Context = context,
                State = ValidationState.Error,
                ErrorMessage = errorMessage,
                Summary = "Validierung fehlgeschlagen."
            };
        }
    }

    /// <summary>
    /// Informationen über einen ähnlichen historischen Wert.
    /// </summary>
    public class SimilarValueInfo
    {
        /// <summary>
        /// Der historische Wert.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Datum des historischen Wertes.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Ähnlichkeits-Score (0-1).
        /// </summary>
        public double Similarity { get; set; }

        /// <summary>
        /// Kontext-Beschreibung.
        /// </summary>
        public string? Description { get; set; }
    }
}
