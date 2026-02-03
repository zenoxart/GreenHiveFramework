using System;
using System.Collections.Generic;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Ergebnis einer Validierung
    /// </summary>
    public class ValidationResult
    {
        public ValidationState State { get; set; }
        public double Confidence { get; set; }
        public double? ExpectedValue { get; set; }
        public double? ActualValue { get; set; }
        public double? DeviationPercentage { get; set; }
        public string? Message { get; set; }
        public string? Details { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public ValidationSeverity Severity => State switch
        {
            ValidationState.Valid => ValidationSeverity.Success,
            ValidationState.Warning => ValidationSeverity.Warning,
            ValidationState.Anomaly => ValidationSeverity.Error,
            ValidationState.Error => ValidationSeverity.Error,
            _ => ValidationSeverity.Info
        };

        public static ValidationResult Success(double? actual = null, string? message = null)
        {
            return new ValidationResult
            {
                State = ValidationState.Valid,
                Confidence = 1.0,
                ActualValue = actual,
                Message = message ?? "Wert ist plausibel"
            };
        }

        public static ValidationResult Warning(double? actual, double? expected, double deviation, string? message = null)
        {
            return new ValidationResult
            {
                State = ValidationState.Warning,
                ActualValue = actual,
                ExpectedValue = expected,
                DeviationPercentage = deviation,
                Message = message ?? "Leichte Abweichung vom Erwartungswert"
            };
        }

        public static ValidationResult Anomaly(double? actual, double? expected, double deviation, string? message = null)
        {
            return new ValidationResult
            {
                State = ValidationState.Anomaly,
                ActualValue = actual,
                ExpectedValue = expected,
                DeviationPercentage = deviation,
                Message = message ?? "Starke Abweichung erkannt!"
            };
        }

        public static ValidationResult Error(string errorMessage)
        {
            return new ValidationResult
            {
                State = ValidationState.Error,
                ErrorMessage = errorMessage,
                Message = "Validierung fehlgeschlagen"
            };
        }
    }
}
