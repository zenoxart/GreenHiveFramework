using System.Windows.Controls;
using Zenox.NET.Core.Common.Notifications;

namespace Zenox.Wpf.Controls.Common.Notifications
{
	public partial class NotificationContainer : UserControl
	{
		public NotificationContainer()
		{
			InitializeComponent();

			// Subscribe zu NotificationService
			NotificationService.Instance.NotificationAdded += OnNotificationAdded;
			NotificationService.Instance.NotificationRemoved += OnNotificationRemoved;
		}

		private void OnNotificationAdded(object? sender, NotificationItem notification)
		{
			Dispatcher.Invoke(() =>
			{
				var toast = new NotificationToast();
				toast.SetNotification(notification);
				toast.CloseRequested += (s, e) => RemoveToast(toast, notification.Id);
				toast.IsHitTestVisible = true;

				NotificationsItemsControl.Items.Add(toast);
			});
		}

		private void OnNotificationRemoved(object? sender, NotificationItem notification)
		{
			Dispatcher.Invoke(() =>
			{
				// Finde und entferne Toast
				foreach (var item in NotificationsItemsControl.Items)
				{
					if (item is NotificationToast toast && toast.Notification?.Id == notification.Id)
					{
						RemoveToast(toast, notification.Id);
						break;
					}
				}
			});
		}

		private void RemoveToast(NotificationToast toast, Guid notificationId)
		{
			NotificationsItemsControl.Items.Remove(toast);
			NotificationService.Instance.Remove(notificationId);
		}
	}
}
