namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Validierungsergebnis-Status
    /// </summary>
    public enum ValidationState
    {
        Valid,
        Warning,
        Anomaly,
        Error
    }

    /// <summary>
    /// Schweregrad für Feedback-Anzeige
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Optionen für visuelles Feedback
    /// </summary>
    [Flags]
    public enum ValidationFeedbackOptions
    {
        None = 0,
        BorderHighlight = 1,
        Toast = 2,
        Pulse = 4,
        Default = BorderHighlight | Toast
    }

    /// <summary>
    /// Sensitivität der Anomalie-Erkennung
    /// </summary>
    public enum AnomalySensitivity
    {
        Low = 30,
        Normal = 20,
        High = 10
    }
}
