using System.Collections.ObjectModel;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.NotificationClient;

/// <summary>One entry of the notification centre.</summary>
public sealed class InAppNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Title { get; init; }

    public string? Message { get; init; }

    public NotificationSeverity Severity { get; init; }

    public DateTimeOffset Time { get; init; } = DateTimeOffset.Now;

    public string? NavigationTarget { get; init; }

    public string? NavigationParameter { get; init; }

    public string? EventType { get; init; }

    public bool IsRead { get; set; }
}

/// <summary>
/// The notification centre: visible events (those with a title) plus local messages of the app and the modules.
/// Raises <see cref="Added"/> for the shell's banner and for Windows toasts while the app is in the background.
/// </summary>
public sealed class InAppNotificationService : AppObject
{
    private const int MaxEntries = 200;

    public event EventHandler<InAppNotification>? Added;

    public event EventHandler? Changed;

    public ObservableCollection<InAppNotification> Items { get; } = [];

    public int UnreadCount => Items.Count(i => !i.IsRead);

    public void Show(string title, string? message = null, NotificationSeverity severity = NotificationSeverity.Info,
        string? navigationTarget = null, string? navigationParameter = null)
    {
        var languages = AppContext.Languages;
        var item = new InAppNotification
        {
            Title = ErrorMessages.Text(languages, title),
            Message = message is null ? null : ErrorMessages.Text(languages, message),
            Severity = severity,
            NavigationTarget = navigationTarget,
            NavigationParameter = navigationParameter,
        };
        AppContext.Dispatcher?.Run(() => Insert(item));
    }

    public void ShowEnvelope(NotificationEnvelope envelope)
    {
        if (envelope.Title is null)
        {
            return;
        }

        var languages = AppContext.Languages;
        Insert(new InAppNotification
        {
            Title = ErrorMessages.Text(languages, envelope.Title),
            Message = envelope.Message is null ? null : ErrorMessages.Text(languages, envelope.Message),
            Severity = envelope.Severity,
            Time = envelope.Timestamp == default ? DateTimeOffset.Now : envelope.Timestamp.ToLocalTime(),
            NavigationTarget = envelope.NavigationTarget,
            NavigationParameter = envelope.NavigationParameter,
            EventType = envelope.Type,
        });
    }

    public void MarkAllRead()
    {
        foreach (var item in Items)
        {
            item.IsRead = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Items.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnCreated() =>
        Context<INotificationContext>().Router.Subscribe("*", ShowEnvelope);

    private void Insert(InAppNotification item)
    {
        Items.Insert(0, item);
        while (Items.Count > MaxEntries)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        Added?.Invoke(this, item);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
