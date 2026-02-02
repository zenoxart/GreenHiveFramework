namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Definiert die möglichen Validierungszustände für kontextbewusste ML-Validierung.
    /// </summary>
    public enum ValidationState
    {
        /// <summary>
        /// Initialzustand - noch keine Validierung durchgeführt.
        /// </summary>
        Idle,

        /// <summary>
        /// Validierung läuft gerade.
        /// </summary>
        Validating,

        /// <summary>
        /// Wert ist im Kontext korrekt/erwartbar.
        /// </summary>
        Valid,

        /// <summary>
        /// Wert weicht leicht vom Erwartungswert ab (Warnung).
        /// </summary>
        Warning,

        /// <summary>
        /// Wert weicht stark ab oder ist im Kontext ungewöhnlich.
        /// </summary>
        Anomaly,

        /// <summary>
        /// Validierung fehlgeschlagen (technischer Fehler).
        /// </summary>
        Error,

        /// <summary>
        /// Benutzer hat den Hinweis bestätigt/ignoriert.
        /// </summary>
        Acknowledged
    }

    /// <summary>
    /// Trigger für Zustandsübergänge in der Validierungs-StateMachine.
    /// </summary>
    public enum ValidationTrigger
    {
        /// <summary>
        /// Startet die Validierung (z.B. bei Rechtsklick).
        /// </summary>
        StartValidation,

        /// <summary>
        /// ML-Validierung abgeschlossen - Wert ist korrekt.
        /// </summary>
        ValidationPassed,

        /// <summary>
        /// ML-Validierung abgeschlossen - leichte Abweichung.
        /// </summary>
        ValidationWarning,

        /// <summary>
        /// ML-Validierung abgeschlossen - starke Abweichung/Anomalie.
        /// </summary>
        ValidationFailed,

        /// <summary>
        /// Ein Fehler ist aufgetreten.
        /// </summary>
        ErrorOccurred,

        /// <summary>
        /// Benutzer bestätigt/ignoriert den Hinweis.
        /// </summary>
        Acknowledge,

        /// <summary>
        /// Zurücksetzen auf Initialzustand.
        /// </summary>
        Reset
    }

    /// <summary>
    /// Definiert die Art der visuellen Reaktion auf Validierungsergebnisse.
    /// </summary>
    public enum ValidationFeedbackType
    {
        /// <summary>
        /// Kein visuelles Feedback.
        /// </summary>
        None,

        /// <summary>
        /// Border-Highlighting des Elements.
        /// </summary>
        BorderHighlight,

        /// <summary>
        /// Hintergrund-Highlighting.
        /// </summary>
        BackgroundHighlight,

        /// <summary>
        /// Toast-Benachrichtigung anzeigen.
        /// </summary>
        Toast,

        /// <summary>
        /// Tooltip mit Erklärung anzeigen.
        /// </summary>
        Tooltip,

        /// <summary>
        /// Pulsierender Effekt.
        /// </summary>
        Pulse,

        /// <summary>
        /// Icon-Indikator neben dem Element.
        /// </summary>
        Icon,

        /// <summary>
        /// Alle verfügbaren Feedback-Arten kombiniert.
        /// </summary>
        All
    }

    /// <summary>
    /// Schwellenwert-Konfiguration für Anomalie-Erkennung.
    /// </summary>
    public enum AnomalyThreshold
    {
        /// <summary>
        /// Streng - kleine Abweichungen werden als Anomalie erkannt.
        /// </summary>
        Strict,

        /// <summary>
        /// Normal - moderate Empfindlichkeit.
        /// </summary>
        Normal,

        /// <summary>
        /// Tolerant - nur große Abweichungen werden als Anomalie erkannt.
        /// </summary>
        Tolerant
    }
}
