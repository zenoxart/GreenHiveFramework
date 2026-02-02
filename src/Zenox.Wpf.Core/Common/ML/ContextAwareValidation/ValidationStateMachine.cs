using System;
using Zenox.Wpf.Core.Common.Workflow;
using Zenox.Wpf.Core.Common.MVVM.FactoryInjection;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Spezialisierte StateMachine für Validierungs-Workflows.
    /// Steuert Zustandsübergänge und triggert visuelle Reaktionen basierend auf ML-Validierungsergebnissen.
    /// </summary>
    public class ValidationStateMachine : StateMachine<ValidationState, ValidationTrigger>
    {
        /// <summary>
        /// Event das gefeuert wird, wenn sich der Validierungszustand ändert.
        /// </summary>
        public event EventHandler<ValidationStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Event das gefeuert wird, wenn eine visuelle Reaktion angezeigt werden soll.
        /// </summary>
        public event EventHandler<ValidationFeedbackEventArgs>? FeedbackRequested;

        /// <summary>
        /// Das aktuelle Validierungsergebnis.
        /// </summary>
        public ValidationResult? CurrentResult { get; private set; }

        /// <summary>
        /// Konfiguration für visuelles Feedback.
        /// </summary>
        public ValidationFeedbackConfiguration FeedbackConfig { get; set; } = new();

        /// <summary>
        /// Initialisiert die ValidationStateMachine mit dem Idle-Zustand.
        /// </summary>
        public ValidationStateMachine() : base(ValidationState.Idle)
        {
            ConfigureTransitions();
            ConfigureStateActions();
        }

        /// <summary>
        /// Konfiguriert alle gültigen Zustandsübergänge.
        /// </summary>
        private void ConfigureTransitions()
        {
            // Von Idle aus
            AddTransition(ValidationState.Idle, ValidationTrigger.StartValidation, ValidationState.Validating);

            // Von Validating aus
            AddTransition(ValidationState.Validating, ValidationTrigger.ValidationPassed, ValidationState.Valid);
            AddTransition(ValidationState.Validating, ValidationTrigger.ValidationWarning, ValidationState.Warning);
            AddTransition(ValidationState.Validating, ValidationTrigger.ValidationFailed, ValidationState.Anomaly);
            AddTransition(ValidationState.Validating, ValidationTrigger.ErrorOccurred, ValidationState.Error);

            // Acknowledge von Warning/Anomaly/Error
            AddTransition(ValidationState.Warning, ValidationTrigger.Acknowledge, ValidationState.Acknowledged);
            AddTransition(ValidationState.Anomaly, ValidationTrigger.Acknowledge, ValidationState.Acknowledged);
            AddTransition(ValidationState.Error, ValidationTrigger.Acknowledge, ValidationState.Acknowledged);
            AddTransition(ValidationState.Valid, ValidationTrigger.Acknowledge, ValidationState.Acknowledged);

            // Reset von jedem Zustand
            AddTransition(ValidationState.Valid, ValidationTrigger.Reset, ValidationState.Idle);
            AddTransition(ValidationState.Warning, ValidationTrigger.Reset, ValidationState.Idle);
            AddTransition(ValidationState.Anomaly, ValidationTrigger.Reset, ValidationState.Idle);
            AddTransition(ValidationState.Error, ValidationTrigger.Reset, ValidationState.Idle);
            AddTransition(ValidationState.Acknowledged, ValidationTrigger.Reset, ValidationState.Idle);
            AddTransition(ValidationState.Validating, ValidationTrigger.Reset, ValidationState.Idle);

            // Erneute Validierung von Acknowledged
            AddTransition(ValidationState.Acknowledged, ValidationTrigger.StartValidation, ValidationState.Validating);
        }

        /// <summary>
        /// Konfiguriert Aktionen beim Eintritt/Verlassen von Zuständen.
        /// </summary>
        private void ConfigureStateActions()
        {
            OnEnterState(ValidationState.Validating, () =>
            {
                RaiseFeedback(ValidationFeedbackType.Pulse, "Validierung läuft...");
            });

            OnEnterState(ValidationState.Valid, () =>
            {
                if (FeedbackConfig.ShowValidFeedback)
                {
                    RaiseFeedback(ValidationFeedbackType.BorderHighlight, 
                        CurrentResult?.Summary ?? "Wert ist plausibel", "success");
                }
            });

            OnEnterState(ValidationState.Warning, () =>
            {
                RaiseFeedback(FeedbackConfig.WarningFeedbackType,
                    CurrentResult?.Summary ?? "Leichte Abweichung erkannt", "warning");
                
                if (FeedbackConfig.ShowToastOnWarning)
                {
                    RaiseFeedback(ValidationFeedbackType.Toast,
                        CurrentResult?.Explanation ?? "Der Wert weicht vom Erwartungswert ab.", "warning");
                }
            });

            OnEnterState(ValidationState.Anomaly, () =>
            {
                RaiseFeedback(FeedbackConfig.AnomalyFeedbackType,
                    CurrentResult?.Summary ?? "Starke Abweichung erkannt!", "error");
                
                if (FeedbackConfig.ShowToastOnAnomaly)
                {
                    RaiseFeedback(ValidationFeedbackType.Toast,
                        CurrentResult?.Explanation ?? "Der Wert weicht stark vom Erwartungswert ab!", "error");
                }
            });

            OnEnterState(ValidationState.Error, () =>
            {
                RaiseFeedback(ValidationFeedbackType.Toast,
                    CurrentResult?.ErrorMessage ?? "Validierung fehlgeschlagen", "error");
            });

            OnEnterState(ValidationState.Acknowledged, () =>
            {
                // Visuelles Feedback zurücksetzen nach kurzer Zeit
                RaiseFeedback(ValidationFeedbackType.None, "Acknowledged");
            });

            OnExitState(ValidationState.Validating, () =>
            {
                // Pulse-Animation stoppen
            });
        }

        /// <summary>
        /// Startet die Validierung und wechselt in den Validating-Zustand.
        /// </summary>
        public bool StartValidation()
        {
            CurrentResult = null;
            return Fire(ValidationTrigger.StartValidation);
        }

        /// <summary>
        /// Verarbeitet ein Validierungsergebnis und triggert den entsprechenden Zustandsübergang.
        /// </summary>
        /// <param name="result">Das ML-Validierungsergebnis.</param>
        public bool ProcessResult(ValidationResult result)
        {
            CurrentResult = result;

            var trigger = result.State switch
            {
                ValidationState.Valid => ValidationTrigger.ValidationPassed,
                ValidationState.Warning => ValidationTrigger.ValidationWarning,
                ValidationState.Anomaly => ValidationTrigger.ValidationFailed,
                ValidationState.Error => ValidationTrigger.ErrorOccurred,
                _ => ValidationTrigger.ErrorOccurred
            };

            var success = Fire(trigger);

            if (success)
            {
                StateChanged?.Invoke(this, new ValidationStateChangedEventArgs(
                    State, result, trigger));
            }

            return success;
        }

        /// <summary>
        /// Bestätigt/Ignoriert den aktuellen Hinweis.
        /// </summary>
        public bool Acknowledge()
        {
            return Fire(ValidationTrigger.Acknowledge);
        }

        /// <summary>
        /// Setzt die StateMachine auf den Initialzustand zurück.
        /// </summary>
        public bool Reset()
        {
            CurrentResult = null;
            return Fire(ValidationTrigger.Reset);
        }

        /// <summary>
        /// Löst ein Feedback-Event aus.
        /// </summary>
        private void RaiseFeedback(ValidationFeedbackType type, string message, string severity = "info")
        {
            FeedbackRequested?.Invoke(this, new ValidationFeedbackEventArgs(type, message, severity, CurrentResult));
        }
    }

    /// <summary>
    /// EventArgs für Zustandsänderungen.
    /// </summary>
    public class ValidationStateChangedEventArgs : EventArgs
    {
        public ValidationState NewState { get; }
        public ValidationResult? Result { get; }
        public ValidationTrigger Trigger { get; }

        public ValidationStateChangedEventArgs(ValidationState newState, ValidationResult? result, ValidationTrigger trigger)
        {
            NewState = newState;
            Result = result;
            Trigger = trigger;
        }
    }

    /// <summary>
    /// EventArgs für Feedback-Anforderungen.
    /// </summary>
    public class ValidationFeedbackEventArgs : EventArgs
    {
        public ValidationFeedbackType FeedbackType { get; }
        public string Message { get; }
        public string Severity { get; }
        public ValidationResult? Result { get; }

        public ValidationFeedbackEventArgs(ValidationFeedbackType feedbackType, string message, 
            string severity, ValidationResult? result)
        {
            FeedbackType = feedbackType;
            Message = message;
            Severity = severity;
            Result = result;
        }
    }

    /// <summary>
    /// Konfiguration für visuelles Feedback.
    /// </summary>
    public class ValidationFeedbackConfiguration
    {
        /// <summary>
        /// Zeigt Feedback auch bei gültigen Werten an.
        /// </summary>
        public bool ShowValidFeedback { get; set; } = false;

        /// <summary>
        /// Zeigt Toast bei Warnungen.
        /// </summary>
        public bool ShowToastOnWarning { get; set; } = true;

        /// <summary>
        /// Zeigt Toast bei Anomalien.
        /// </summary>
        public bool ShowToastOnAnomaly { get; set; } = true;

        /// <summary>
        /// Feedback-Typ für Warnungen.
        /// </summary>
        public ValidationFeedbackType WarningFeedbackType { get; set; } = ValidationFeedbackType.BorderHighlight;

        /// <summary>
        /// Feedback-Typ für Anomalien.
        /// </summary>
        public ValidationFeedbackType AnomalyFeedbackType { get; set; } = ValidationFeedbackType.All;

        /// <summary>
        /// Dauer des Highlights in Millisekunden.
        /// </summary>
        public int HighlightDurationMs { get; set; } = 5000;

        /// <summary>
        /// Automatisches Acknowledge nach x Millisekunden (0 = deaktiviert).
        /// </summary>
        public int AutoAcknowledgeMs { get; set; } = 0;
    }
}
