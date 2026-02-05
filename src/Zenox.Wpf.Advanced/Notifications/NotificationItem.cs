namespace Zenox.NET.Core.Common.Notifications
{
	/// <summary>
	/// Repräsentiert eine einzelne Benachrichtigung
	/// </summary>
	public class NotificationItem
	{
		/// <summary>
		/// Eindeutige ID der Benachrichtigung
		/// </summary>
		public Guid Id { get; set; } = Guid.NewGuid();

		/// <summary>
		/// Titel der Benachrichtigung
		/// </summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>
		/// Nachrichtentext der Benachrichtigung
		/// </summary>
		public string Message { get; set; } = string.Empty;

		/// <summary>
		/// Typ der Benachrichtigung
		/// </summary>
		public NotificationType Type { get; set; } = NotificationType.Info;

		/// <summary>
		/// Zeitstempel der Erstellung
		/// </summary>
		public DateTime Timestamp { get; set; } = DateTime.Now;

		/// <summary>
		/// Dauer in Sekunden, wie lange die Benachrichtigung angezeigt wird (0 = unendlich)
		/// </summary>
		public int DurationSeconds { get; set; } = 5;

		/// <summary>
		/// Optionale Action, die beim Klick auf die Benachrichtigung ausgeführt wird
		/// </summary>
		public Action? ClickAction { get; set; }
	}
}
