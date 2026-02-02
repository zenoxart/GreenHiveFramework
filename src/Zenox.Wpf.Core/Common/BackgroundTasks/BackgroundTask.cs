using System.Diagnostics;

namespace Zenox.Wpf.Core.Common.BackgroundTasks
{
	/// <summary>
	/// Repräsentiert eine Hintergrundaufgabe
	/// </summary>
	public class BackgroundTask
	{
		private readonly Stopwatch _stopwatch;

		public BackgroundTask(string name, Func<CancellationToken, Task> action, TaskPriority priority = TaskPriority.Normal)
		{
			Id = Guid.NewGuid();
			Name = name;
			Action = action;
			Priority = priority;
			Status = TaskStatus.Pending;
			CreatedAt = DateTime.Now;
			_stopwatch = new Stopwatch();
		}

		/// <summary>
		/// Eindeutige ID der Aufgabe
		/// </summary>
		public Guid Id { get; }

		/// <summary>
		/// Name der Aufgabe
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Beschreibung der Aufgabe
		/// </summary>
		public string? Description { get; set; }

		/// <summary>
		/// Priorität der Aufgabe
		/// </summary>
		public TaskPriority Priority { get; set; }

		/// <summary>
		/// Aktueller Status der Aufgabe
		/// </summary>
		public TaskStatus Status { get; set; }

		/// <summary>
		/// Die auszuführende Aktion
		/// </summary>
		public Func<CancellationToken, Task> Action { get; }

		/// <summary>
		/// Zeitpunkt der Erstellung
		/// </summary>
		public DateTime CreatedAt { get; }

		/// <summary>
		/// Zeitpunkt des Starts
		/// </summary>
		public DateTime? StartedAt { get; private set; }

		/// <summary>
		/// Zeitpunkt der Fertigstellung
		/// </summary>
		public DateTime? CompletedAt { get; private set; }

		/// <summary>
		/// Ausführungsdauer in Millisekunden
		/// </summary>
		public long ExecutionTimeMs => _stopwatch.ElapsedMilliseconds;

		/// <summary>
		/// Fehler bei der Ausführung
		/// </summary>
		public Exception? Error { get; private set; }

		/// <summary>
		/// Anzahl der Wiederholungsversuche
		/// </summary>
		public int RetryCount { get; set; }

		/// <summary>
		/// Maximale Anzahl von Wiederholungsversuchen
		/// </summary>
		public int MaxRetries { get; set; } = 3;

		/// <summary>
		/// Callback bei erfolgreicher Ausführung
		/// </summary>
		public Action<BackgroundTask>? OnCompleted { get; set; }

		/// <summary>
		/// Callback bei Fehler
		/// </summary>
		public Action<BackgroundTask, Exception>? OnError { get; set; }

		/// <summary>
		/// Startet die Aufgabe
		/// </summary>
		internal void Start()
		{
			StartedAt = DateTime.Now;
			Status = TaskStatus.Running;
			_stopwatch.Start();
		}

		/// <summary>
		/// Markiert die Aufgabe als abgeschlossen
		/// </summary>
		internal void Complete()
		{
			_stopwatch.Stop();
			CompletedAt = DateTime.Now;
			Status = TaskStatus.Completed;
			OnCompleted?.Invoke(this);
		}

		/// <summary>
		/// Markiert die Aufgabe als fehlgeschlagen
		/// </summary>
		internal void Fail(Exception exception)
		{
			_stopwatch.Stop();
			CompletedAt = DateTime.Now;
			Status = TaskStatus.Failed;
			Error = exception;
			OnError?.Invoke(this, exception);
		}

		/// <summary>
		/// Bricht die Aufgabe ab
		/// </summary>
		internal void Cancel()
		{
			_stopwatch.Stop();
			CompletedAt = DateTime.Now;
			Status = TaskStatus.Cancelled;
		}
	}
}
