using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Zenox.NET.Core.Common.Notifications;

namespace Zenox.Wpf.Controls.Common.Notifications
{
	public partial class NotificationToast : UserControl
	{
		public NotificationItem? Notification { get; set; }

		public event EventHandler? CloseRequested;

		public NotificationToast()
		{
			InitializeComponent();
			Loaded += NotificationToast_Loaded;
		}

		public void SetNotification(NotificationItem notification)
		{
			Notification = notification;
			TitleText.Text = notification.Title;
			MessageText.Text = notification.Message;

			// Style basierend auf Typ
			switch (notification.Type)
			{
				case NotificationType.Info:
					IconBackground.Color = Color.FromRgb(147, 112, 219); // MediumPurple
					IconText.Text = "?";
					break;
				case NotificationType.Success:
					IconBackground.Color = Color.FromRgb(16, 124, 16); // #107C10
					IconText.Text = "?";
					break;
				case NotificationType.Warning:
					IconBackground.Color = Color.FromRgb(255, 185, 0); // #FFB900
					IconText.Text = "?";
					break;
				case NotificationType.Error:
					IconBackground.Color = Color.FromRgb(232, 17, 35); // #E81123
					IconText.Text = "?";
					break;
			}
		}

		private void NotificationToast_Loaded(object sender, RoutedEventArgs e)
		{
			// Fade In Animation starten
			var storyboard = FindResource("FadeInStoryboard") as Storyboard;
			storyboard?.Begin();
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			FadeOutAndClose();
		}

		protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);

			// Führe Click-Action aus falls vorhanden
			Notification?.ClickAction?.Invoke();
			FadeOutAndClose();
		}

		private void FadeOutAndClose()
		{
			var storyboard = FindResource("FadeOutStoryboard") as Storyboard;
			storyboard?.Begin();
		}

		private void FadeOut_Completed(object? sender, EventArgs e)
		{
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
	}
}
