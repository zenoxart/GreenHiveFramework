namespace GreenHive.Wpf.Framework.NotificationClient;

/// <summary>Application contexts that offer the notification services.</summary>
public interface INotificationContext
{
    NotificationHubClient Hub { get; }

    NotificationRouter Router { get; }

    InAppNotificationService InApp { get; }
}
