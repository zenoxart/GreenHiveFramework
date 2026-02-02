using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Zenox.Wpf.Core.Common.Notifications;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Manager für visuelles Feedback bei Validierungen.
    /// Zeigt Highlights, Toasts, Tooltips etc. basierend auf Validierungsergebnissen.
    /// </summary>
    public class ValidationFeedbackManager
    {
        /// <summary>
        /// Zeigt Feedback für ein validiertes Element an.
        /// </summary>
        public void ShowFeedback(FrameworkElement element, ValidationResult result, ValidationStateMachine stateMachine)
        {
            if (element == null || result == null)
                return;

            // StateMachine-Events abonnieren für dynamisches Feedback
            stateMachine.FeedbackRequested += (s, e) => HandleFeedbackRequest(element, e);

            // Initiales Feedback basierend auf Ergebnis
            switch (result.State)
            {
                case ValidationState.Valid:
                    ShowValidFeedback(element, result);
                    break;

                case ValidationState.Warning:
                    ShowWarningFeedback(element, result);
                    break;

                case ValidationState.Anomaly:
                    ShowAnomalyFeedback(element, result);
                    break;

                case ValidationState.Error:
                    ShowErrorFeedback(element, result);
                    break;
            }
        }

        /// <summary>
        /// Behandelt Feedback-Anfragen von der StateMachine.
        /// </summary>
        private void HandleFeedbackRequest(FrameworkElement element, ValidationFeedbackEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                switch (e.FeedbackType)
                {
                    case ValidationFeedbackType.BorderHighlight:
                        ApplyBorderHighlight(element, GetColorForSeverity(e.Severity));
                        break;

                    case ValidationFeedbackType.BackgroundHighlight:
                        ApplyBackgroundHighlight(element, GetColorForSeverity(e.Severity));
                        break;

                    case ValidationFeedbackType.Pulse:
                        ApplyPulseAnimation(element);
                        break;

                    case ValidationFeedbackType.Toast:
                        ShowToast("Validierung", e.Message, e.Severity);
                        break;

                    case ValidationFeedbackType.Tooltip:
                        ShowTooltip(element, e.Message);
                        break;

                    case ValidationFeedbackType.Icon:
                        ShowIconIndicator(element, e.Severity);
                        break;

                    case ValidationFeedbackType.None:
                        ClearFeedback(element);
                        break;

                    case ValidationFeedbackType.All:
                        ApplyBorderHighlight(element, GetColorForSeverity(e.Severity));
                        ApplyPulseAnimation(element);
                        ShowToast("Validierung", e.Message, e.Severity);
                        break;
                }
            });
        }

        /// <summary>
        /// Zeigt Feedback für gültige Werte.
        /// </summary>
        private void ShowValidFeedback(FrameworkElement element, ValidationResult result)
        {
            ApplyBorderHighlight(element, Colors.Green, duration: 1500);
            
            ShowToast(
                "? Validierung erfolgreich",
                result.Summary ?? "Der Wert ist plausibel.",
                "success");
        }

        /// <summary>
        /// Zeigt Feedback für Warnungen.
        /// </summary>
        private void ShowWarningFeedback(FrameworkElement element, ValidationResult result)
        {
            ApplyBorderHighlight(element, Colors.Orange, duration: 3000);
            ApplyPulseAnimation(element, intensity: 0.85);

            var message = BuildWarningMessage(result);
            ShowToast("? Leichte Abweichung", message, "warning", duration: 6);
        }

        /// <summary>
        /// Zeigt Feedback für Anomalien.
        /// </summary>
        private void ShowAnomalyFeedback(FrameworkElement element, ValidationResult result)
        {
            ApplyBorderHighlight(element, Colors.Red, duration: 5000, thickness: 3);
            ApplyPulseAnimation(element, intensity: 0.7, repeatCount: 5);

            var message = BuildAnomalyMessage(result);
            ShowToast("?? Anomalie erkannt", message, "error", duration: 10);
        }

        /// <summary>
        /// Zeigt Feedback für Fehler.
        /// </summary>
        private void ShowErrorFeedback(FrameworkElement element, ValidationResult result)
        {
            ApplyBorderHighlight(element, Colors.DarkRed, duration: 3000);
            
            ShowToast(
                "? Validierungsfehler",
                result.ErrorMessage ?? "Ein Fehler ist aufgetreten.",
                "error");
        }

        /// <summary>
        /// Erstellt eine Warnungs-Nachricht.
        /// </summary>
        private string BuildWarningMessage(ValidationResult result)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(result.Summary))
                parts.Add(result.Summary);

            if (result.ExpectedValue.HasValue)
                parts.Add($"Erwarteter Wert: ~{result.ExpectedValue:F2}");

            if (result.ActualValue.HasValue)
                parts.Add($"Tatsächlicher Wert: {result.ActualValue:F2}");

            if (result.DeviationPercentage.HasValue)
                parts.Add($"Abweichung: {result.DeviationPercentage:F1}%");

            if (result.Recommendations.Count > 0)
                parts.Add($"Tipp: {result.Recommendations[0]}");

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Erstellt eine Anomalie-Nachricht.
        /// </summary>
        private string BuildAnomalyMessage(ValidationResult result)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(result.Summary))
                parts.Add($"? {result.Summary}");

            if (!string.IsNullOrEmpty(result.Explanation))
                parts.Add(result.Explanation);

            if (result.ExpectedValue.HasValue && result.ActualValue.HasValue)
            {
                parts.Add($"\nErwartet: ~{result.ExpectedValue:F2}");
                parts.Add($"Tatsächlich: {result.ActualValue:F2}");
            }

            if (result.DeviationPercentage.HasValue)
                parts.Add($"Abweichung: {result.DeviationPercentage:F1}%");

            if (result.Recommendations.Count > 0)
            {
                parts.Add("\nEmpfehlungen:");
                foreach (var rec in result.Recommendations)
                {
                    parts.Add($"• {rec}");
                }
            }

            if (result.SimilarValues.Count > 0)
            {
                parts.Add("\nÄhnliche historische Werte:");
                foreach (var similar in result.SimilarValues)
                {
                    parts.Add($"• {similar.Value:F2} ({similar.Date:d})");
                }
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Wendet Border-Highlighting an.
        /// </summary>
        private void ApplyBorderHighlight(FrameworkElement element, Color color, int duration = 3000, double thickness = 2)
        {
            var brush = new SolidColorBrush(color);

            if (element is Control control)
            {
                var originalBrush = control.BorderBrush;
                var originalThickness = control.BorderThickness;

                control.BorderBrush = brush;
                control.BorderThickness = new Thickness(thickness);

                // Fade-out Animation
                var colorAnim = new ColorAnimation
                {
                    To = Colors.Transparent,
                    Duration = TimeSpan.FromMilliseconds(duration),
                    BeginTime = TimeSpan.FromMilliseconds(500)
                };

                colorAnim.Completed += (s, e) =>
                {
                    control.BorderBrush = originalBrush;
                    control.BorderThickness = originalThickness;
                };

                brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
            }
            else if (element is Border border)
            {
                var originalBrush = border.BorderBrush;
                var originalThickness = border.BorderThickness;

                border.BorderBrush = brush;
                border.BorderThickness = new Thickness(thickness);

                var colorAnim = new ColorAnimation
                {
                    To = Colors.Transparent,
                    Duration = TimeSpan.FromMilliseconds(duration),
                    BeginTime = TimeSpan.FromMilliseconds(500)
                };

                colorAnim.Completed += (s, e) =>
                {
                    border.BorderBrush = originalBrush;
                    border.BorderThickness = originalThickness;
                };

                brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
            }
        }

        /// <summary>
        /// Wendet Hintergrund-Highlighting an.
        /// </summary>
        private void ApplyBackgroundHighlight(FrameworkElement element, Color color, int duration = 2000)
        {
            var highlightColor = Color.FromArgb(80, color.R, color.G, color.B);
            var brush = new SolidColorBrush(highlightColor);

            Brush? originalBrush = null;

            if (element is Control control)
            {
                originalBrush = control.Background;
                control.Background = brush;
            }
            else if (element is Panel panel)
            {
                originalBrush = panel.Background;
                panel.Background = brush;
            }

            // Fade-out
            var anim = new ColorAnimation
            {
                To = Colors.Transparent,
                Duration = TimeSpan.FromMilliseconds(duration)
            };

            anim.Completed += (s, e) =>
            {
                if (element is Control ctrl)
                    ctrl.Background = originalBrush;
                else if (element is Panel pnl)
                    pnl.Background = originalBrush;
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        /// <summary>
        /// Wendet Pulse-Animation an.
        /// </summary>
        private void ApplyPulseAnimation(FrameworkElement element, double intensity = 0.7, int repeatCount = 3)
        {
            var anim = new DoubleAnimation
            {
                From = 1.0,
                To = intensity,
                Duration = TimeSpan.FromMilliseconds(200),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(repeatCount)
            };

            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        /// <summary>
        /// Zeigt ein Toast an.
        /// </summary>
        private void ShowToast(string title, string message, string severity, int duration = 5)
        {
            switch (severity.ToLower())
            {
                case "success":
                    NotificationService.Instance?.ShowSuccess(title, message, duration);
                    break;
                case "warning":
                    NotificationService.Instance?.ShowWarning(title, message, duration);
                    break;
                case "error":
                    NotificationService.Instance?.ShowError(title, message, duration);
                    break;
                default:
                    NotificationService.Instance?.ShowInfo(title, message, duration);
                    break;
            }
        }

        /// <summary>
        /// Zeigt einen Tooltip an.
        /// </summary>
        private void ShowTooltip(FrameworkElement element, string message)
        {
            var tooltip = new ToolTip
            {
                Content = message,
                IsOpen = true,
                StaysOpen = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
            };

            element.ToolTip = tooltip;

            // Auto-close nach 5 Sekunden
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (s, e) =>
            {
                tooltip.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }

        /// <summary>
        /// Zeigt einen Icon-Indikator neben dem Element.
        /// </summary>
        private void ShowIconIndicator(FrameworkElement element, string severity)
        {
            // Placeholder - könnte ein Adorner sein
            // Für jetzt nutzen wir Tooltip
            var icon = severity switch
            {
                "success" => "?",
                "warning" => "?",
                "error" => "?",
                _ => "?"
            };

            ShowTooltip(element, icon);
        }

        /// <summary>
        /// Löscht alle visuellen Feedback-Effekte.
        /// </summary>
        private void ClearFeedback(FrameworkElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            
            if (element is Control control)
            {
                control.BorderBrush?.BeginAnimation(SolidColorBrush.ColorProperty, null);
                control.Background?.BeginAnimation(SolidColorBrush.ColorProperty, null);
            }

            if (element.ToolTip is ToolTip tooltip)
            {
                tooltip.IsOpen = false;
            }
        }

        /// <summary>
        /// Gibt die Farbe für einen Severity-Level zurück.
        /// </summary>
        private Color GetColorForSeverity(string severity)
        {
            return severity?.ToLower() switch
            {
                "success" => Colors.Green,
                "warning" => Colors.Orange,
                "error" => Colors.Red,
                _ => Colors.Blue
            };
        }
    }
}
