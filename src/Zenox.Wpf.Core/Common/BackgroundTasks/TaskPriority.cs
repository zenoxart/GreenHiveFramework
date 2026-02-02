namespace Zenox.Wpf.Core.Common.BackgroundTasks
{
	/// <summary>
	/// Definiert die Priorität einer Hintergrundaufgabe
	/// </summary>
	public enum TaskPriority
	{
		/// <summary>
		/// Niedrige Priorität - wird ausgeführt wenn System idle ist
		/// </summary>
		Low = 0,

		/// <summary>
		/// Normale Priorität - Standard für die meisten Tasks
		/// </summary>
		Normal = 1,

		/// <summary>
		/// Hohe Priorität - wichtige Tasks die schnell ausgeführt werden sollen
		/// </summary>
		High = 2,

		/// <summary>
		/// Kritische Priorität - muss sofort ausgeführt werden
		/// </summary>
		Critical = 3
	}

	/// <summary>
	/// Status einer Hintergrundaufgabe
	/// </summary>
	public enum TaskStatus
	{
		/// <summary>
		/// Task ist in der Warteschlange
		/// </summary>
		Pending,

		/// <summary>
		/// Task wird gerade ausgeführt
		/// </summary>
		Running,

		/// <summary>
		/// Task wurde erfolgreich abgeschlossen
		/// </summary>
		Completed,

		/// <summary>
		/// Task ist fehlgeschlagen
		/// </summary>
		Failed,

		/// <summary>
		/// Task wurde abgebrochen
		/// </summary>
		Cancelled
	}
}
