using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace GreenHive.NET.Core.Common.Notifications
{
	/// <summary>
	/// Singleton Service für Toast-Benachrichtigungen
	/// </summary>
	public sealed class NotificationService
	{
		private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());

		/// <summary>
		/// Singleton-Instanz des NotificationService
		/// </summary>
		public static NotificationService Instance => _instance.Value;

		private readonly ObservableCollection<NotificationItem> _activeNotifications = new();
		private readonly DispatcherTimer _cleanupTimer;

		/// <summary>
		/// Aktive Benachrichtigungen
		/// </summary>
		public ReadOnlyObservableCollection<NotificationItem> ActiveNotifications { get; }

		/// <summary>
		/// Event das gefeuert wird, wenn eine neue Benachrichtigung hinzugefügt wird
		/// </summary>
		public event EventHandler<NotificationItem>? NotificationAdded;

		/// <summary>
		/// Event das gefeuert wird, wenn eine Benachrichtigung entfernt wird
		/// </summary>
		public event EventHandler<NotificationItem>? NotificationRemoved;

		/// <summary>
		/// Maximale Anzahl gleichzeitig angezeigter Benachrichtigungen
		/// </summary>
		public int MaxActiveNotifications { get; set; } = 5;

		private NotificationService()
		{
			ActiveNotifications = new ReadOnlyObservableCollection<NotificationItem>(_activeNotifications);

			// Cleanup-Timer für Auto-Dismiss
			_cleanupTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			_cleanupTimer.Tick += CleanupTimer_Tick;
			_cleanupTimer.Start();
		}

		/// <summary>
		/// Zeigt eine Informations-Benachrichtigung an
		/// </summary>
		public void ShowInfo(string title, string message, int durationSeconds = 5, Action? clickAction = null)
		{
			Show(title, message, NotificationType.Info, durationSeconds, clickAction);
		}

		/// <summary>
		/// Zeigt eine Erfolgs-Benachrichtigung an
		/// </summary>
		public void ShowSuccess(string title, string message, int durationSeconds = 5, Action? clickAction = null)
		{
			Show(title, message, NotificationType.Success, durationSeconds, clickAction);
		}

		/// <summary>
		/// Zeigt eine Warnungs-Benachrichtigung an
		/// </summary>
		public void ShowWarning(string title, string message, int durationSeconds = 5, Action? clickAction = null)
		{
			Show(title, message, NotificationType.Warning, durationSeconds, clickAction);
		}

		/// <summary>
		/// Zeigt eine Fehler-Benachrichtigung an
		/// </summary>
		public void ShowError(string title, string message, int durationSeconds = 8, Action? clickAction = null)
		{
			Show(title, message, NotificationType.Error, durationSeconds, clickAction);
		}

		/// <summary>
		/// Zeigt eine Benachrichtigung an
		/// </summary>
		public void Show(string title, string message, NotificationType type = NotificationType.Info,
			int durationSeconds = 5, Action? clickAction = null)
		{
			var notification = new NotificationItem
			{
				Title = title,
				Message = message,
				Type = type,
				DurationSeconds = durationSeconds,
				ClickAction = clickAction
			};

			System.Windows.Application.Current?.Dispatcher.Invoke(() =>
			{
				// Wenn maximale Anzahl erreicht, entferne älteste
				while (_activeNotifications.Count >= MaxActiveNotifications)
				{
					var oldest = _activeNotifications.FirstOrDefault();
					if (oldest != null)
					{
						Remove(oldest.Id);
					}
				}

				_activeNotifications.Add(notification);
				NotificationAdded?.Invoke(this, notification);

				// Auto-Dismiss nach Dauer
				if (notification.DurationSeconds > 0)
				{
					Task.Delay(TimeSpan.FromSeconds(notification.DurationSeconds))
						.ContinueWith(_ => Remove(notification.Id));
				}
			});
		}

		/// <summary>
		/// Entfernt eine Benachrichtigung
		/// </summary>
		public void Remove(Guid notificationId)
		{
			System.Windows.Application.Current?.Dispatcher.Invoke(() =>
			{
				var notification = _activeNotifications.FirstOrDefault(n => n.Id == notificationId);
				if (notification != null)
				{
					_activeNotifications.Remove(notification);
					NotificationRemoved?.Invoke(this, notification);
				}
			});
		}

		/// <summary>
		/// Entfernt alle Benachrichtigungen
		/// </summary>
		public void Clear()
		{
			System.Windows.Application.Current?.Dispatcher.Invoke(() =>
			{
				var notifications = _activeNotifications.ToList();
				_activeNotifications.Clear();

				foreach (var notification in notifications)
				{
					NotificationRemoved?.Invoke(this, notification);
				}
			});
		}

		private void CleanupTimer_Tick(object? sender, EventArgs e)
		{
			// Cleanup für abgelaufene Notifications (falls sie nicht automatisch entfernt wurden)
			var expired = _activeNotifications
				.Where(n => n.DurationSeconds > 0 &&
						   (DateTime.Now - n.Timestamp).TotalSeconds > n.DurationSeconds)
				.ToList();

			foreach (var notification in expired)
			{
				Remove(notification.Id);
			}
		}
	}
}
