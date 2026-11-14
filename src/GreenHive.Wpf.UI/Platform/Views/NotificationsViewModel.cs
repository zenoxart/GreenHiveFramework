using GreenHive.Wpf.UI.Platform;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.NotificationClient;

namespace GreenHive.Wpf.UI.Platform.Views;

/// <summary>Notification centre: the in-app notifications (newest first); a click navigates to the referenced view.</summary>
public sealed class NotificationsViewModel : ViewModelAppObject
{
    private ClientAppContext Client => Context<ClientAppContext>();

    public IReadOnlyList<InAppNotification> Items => Client.InApp.Items.ToList();

    public event EventHandler? Changed;

    protected override void OnCreated()
    {
        Title = AppContext.Languages.GetText("shell:view.notifications.label");
        Client.InApp.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public override void OnNavigatedTo(object? parameter) => MarkAllRead();

    public void Open(InAppNotification item)
    {
        item.IsRead = true;
        if (item.NavigationTarget is { } target && Client.Navigation.Registry.Find(target) is not null)
        {
            Client.Navigation.NavigateTo(target, item.NavigationParameter);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkAllRead() => Client.InApp.MarkAllRead();

    public void Clear() => Client.InApp.Clear();

    public string T(string key) => AppContext.Languages.GetText("shell:" + key);
}
