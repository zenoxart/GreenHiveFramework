namespace GreenHive.NET.Core.Common.Tutorial
{
    /// <summary>
    /// Definiert alle Eigenschaften eines Tutorial-Schrittes
    /// </summary>
    public class TutorialStep
    {
        /// <summary>
        /// Gibt und setzt Schlüssel-Name
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gibt und setzt den Titel
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gibt und setzt den Anleitungstext
        /// </summary>
        public string InstructionText { get; set; }

        /// <summary>
        /// Gibt und setzt die Schritt-ARt
        /// </summary>
        public TutorialStepType StepType { get; set; } = TutorialStepType.Highlight;

        /// <summary>
        /// Gibt und setzt die Position des Tutorial-Popups
        /// </summary>
        public TutorialPosition Position { get; set; } = TutorialPosition.Auto;

        /// <summary>
        /// Gibt und setzt die Bedingung der Vervollständigung
        /// </summary>
        public Func<bool>? CompletionCondition { get; set; }

        /// <summary>
        /// Gibt und setzt den Wahrheitswert, ob übersprungen werden kann
        /// </summary>
        public bool CanSkip { get; set; } = true;

        /// <summary>
        /// Gibt und setzt die Verzögerungszeit
        /// </summary>
        public int DelayMs { get; set; } = 0;
    }

    /// <summary>
    /// Definiert die Art wie der Tutorialstep hervorgehoben werden soll
    /// </summary>
    public enum TutorialStepType
    {
        Highlight,      // Highlight element with overlay
        Tooltip,        // Show tooltip only
        Modal,          // Modal dialog
        Pulse           // Pulse animation on element
    }

    /// <summary>
    /// Gibt die Art der Tutorial-Position an
    /// </summary>
    public enum TutorialPosition
    {
        Auto,
        Top,
        Bottom,
        Left,
        Right,
        Center
    }
}