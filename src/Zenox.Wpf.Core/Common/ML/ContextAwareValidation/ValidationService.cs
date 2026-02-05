using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using Zenox.NET.Core.Common.Notifications;

namespace Zenox.NET.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Zentraler Service für kontextbewusste Validierung mit integriertem Feedback
    /// </summary>
    public class ValidationService
    {
        private static ValidationService? _instance;
        private static readonly object _lock = new();

        public static ValidationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ValidationService();
                    }
                }
                return _instance;
            }
        }

        public ValidationFeedbackOptions DefaultFeedback { get; set; } = ValidationFeedbackOptions.Default;
        public AnomalySensitivity Sensitivity { get; set; } = AnomalySensitivity.Normal;

        private ValidationService() { }

        /// <summary>
        /// Validiert einen Wert mit optionalem visuellen Feedback
        /// </summary>
        public async Task<ValidationResult> ValidateAsync(
            ValidationContext context,
            FrameworkElement? element = null,
            ValidationFeedbackOptions? feedbackOptions = null)
        {
            try
            {
                var result = await PerformValidationAsync(context);

                if (element != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ShowFeedback(element, result, feedbackOptions ?? DefaultFeedback);
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                return ValidationResult.Error(ex.Message);
            }
        }

        /// <summary>
        /// Schnellvalidierung für einfache Szenarien
        /// </summary>
        public async Task<ValidationResult> ValidateValueAsync(
            object? value,
            string fieldName,
            FrameworkElement? element = null)
        {
            var context = new ValidationContext
            {
                Value = value,
                ValueAsString = value?.ToString(),
                FieldName = fieldName
            };

            return await ValidateAsync(context, element);
        }

        private async Task<ValidationResult> PerformValidationAsync(ValidationContext context)
        {
            await Task.Delay(100);

            if (!context.IsNumeric)
            {
                return ValidationResult.Success(null, "Wert akzeptiert");
            }

            var actual = context.AsDouble();
            if (!actual.HasValue)
            {
                return ValidationResult.Error("Wert konnte nicht gelesen werden");
            }

            var expected = CalculateExpectedValue(context);
            if (!expected.HasValue)
            {
                return ValidationResult.Success(actual, "Keine Referenzwerte für Vergleich verfügbar");
            }

            var deviation = Math.Abs(actual.Value - expected.Value) / expected.Value * 100;
            var threshold = (double)Sensitivity;

            if (deviation < threshold / 2)
            {
                return ValidationResult.Success(actual, $"Wert liegt im erwarteten Bereich (±{deviation:F1}%)");
            }
            else if (deviation < threshold)
            {
                var result = ValidationResult.Warning(actual, expected, deviation,
                    $"Wert weicht um {deviation:F1}% vom Erwartungswert ab");
                result.Details = $"Erwartet: ~{expected:F2}\nTatsächlich: {actual:F2}";
                result.Recommendations.Add($"Prüfen Sie, ob {actual:F2} korrekt ist");
                return result;
            }
            else
            {
                var result = ValidationResult.Anomaly(actual, expected, deviation,
                    $"Ungewöhnliche Abweichung von {deviation:F1}% erkannt!");
                result.Details = $"Erwartet: ~{expected:F2}\nTatsächlich: {actual:F2}\n\nDies könnte auf einen Fehler hindeuten.";
                result.Recommendations.Add($"Korrigieren Sie den Wert auf ~{expected:F2}");
                result.Recommendations.Add("Oder bestätigen Sie, dass der Wert korrekt ist");
                return result;
            }
        }

        private double? CalculateExpectedValue(ValidationContext context)
        {
            if (context.HistoricalValues.Count > 0)
            {
                double sum = 0;
                int count = 0;
                foreach (var val in context.HistoricalValues)
                {
                    if (val is IConvertible)
                    {
                        sum += Convert.ToDouble(val);
                        count++;
                    }
                }
                if (count > 0) return sum / count;
            }

            if (context.ExpectedMin.HasValue && context.ExpectedMax.HasValue)
            {
                return (context.ExpectedMin.Value + context.ExpectedMax.Value) / 2;
            }

            if (context.RelatedValues.Count > 0)
            {
                foreach (var (key, value) in context.RelatedValues)
                {
                    if (value is IConvertible && key.Contains("preis", StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToDouble(value) * 1.0;
                    }
                }
            }

            return null;
        }

        private void ShowFeedback(
            FrameworkElement element,
            ValidationResult result,
            ValidationFeedbackOptions options)
        {
            if (options == ValidationFeedbackOptions.None)
                return;

            if (options.HasFlag(ValidationFeedbackOptions.BorderHighlight))
            {
                ShowBorderHighlight(element, result);
            }

            if (options.HasFlag(ValidationFeedbackOptions.Pulse))
            {
                ShowPulseAnimation(element, result);
            }

            if (options.HasFlag(ValidationFeedbackOptions.Toast))
            {
                ShowToast(result);
            }
        }

        private void ShowBorderHighlight(FrameworkElement element, ValidationResult result)
        {
            var color = result.State switch
            {
                ValidationState.Valid => Colors.Green,
                ValidationState.Warning => Colors.Orange,
                ValidationState.Anomaly => Colors.Red,
                ValidationState.Error => Colors.DarkRed,
                _ => Colors.Blue
            };

            var duration = result.State switch
            {
                ValidationState.Valid => 1500,
                ValidationState.Warning => 3000,
                ValidationState.Anomaly => 5000,
                _ => 3000
            };

            ApplyBorderAnimation(element, color, duration);
        }

        private void ApplyBorderAnimation(FrameworkElement element, Color color, int duration)
        {
            var brush = new SolidColorBrush(color);

            if (element is Control control)
            {
                var originalBrush = control.BorderBrush;
                var originalThickness = control.BorderThickness;

                control.BorderBrush = brush;
                control.BorderThickness = new Thickness(2);

                var animation = new ColorAnimation
                {
                    To = Colors.Transparent,
                    Duration = TimeSpan.FromMilliseconds(duration),
                    BeginTime = TimeSpan.FromMilliseconds(500)
                };

                animation.Completed += (s, e) =>
                {
                    control.BorderBrush = originalBrush;
                    control.BorderThickness = originalThickness;
                };

                brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
        }

        private void ShowPulseAnimation(FrameworkElement element, ValidationResult result)
        {
            var repeatCount = result.State switch
            {
                ValidationState.Warning => 2,
                ValidationState.Anomaly => 4,
                _ => 1
            };

            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.7,
                Duration = TimeSpan.FromMilliseconds(200),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(repeatCount)
            };

            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void ShowToast(ValidationResult result)
        {
            var title = result.State switch
            {
                ValidationState.Valid => "? Validierung erfolgreich",
                ValidationState.Warning => "? Leichte Abweichung",
                ValidationState.Anomaly => "?? Anomalie erkannt",
                ValidationState.Error => "? Validierungsfehler",
                _ => "Info"
            };

            var message = result.Message ?? "Keine Details verfügbar";
            if (!string.IsNullOrEmpty(result.Details))
            {
                message += "\n\n" + result.Details;
            }

            var severity = result.Severity;
            switch (severity)
            {
                case ValidationSeverity.Success:
                    NotificationService.Instance?.ShowSuccess(title, message, 5);
                    break;
                case ValidationSeverity.Warning:
                    NotificationService.Instance?.ShowWarning(title, message, 8);
                    break;
                case ValidationSeverity.Error:
                    NotificationService.Instance?.ShowError(title, message, 10);
                    break;
                default:
                    NotificationService.Instance?.ShowInfo(title, message, 5);
                    break;
            }
        }
    }
}
