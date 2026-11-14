using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.UI.Platform;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.UI.Platform.Shell;

/// <summary>Main ViewModel of the shell: status bar texts and the active view.</summary>
public sealed class ShellViewModel : ViewModelAppObject
{
    private string _statusText = string.Empty;
    private string _activeTitle = string.Empty;
    private string _sizeClassText = string.Empty;
    private string _languageText = string.Empty;
    private string? _chordHint;
    private string? _startupStatus;
    private string _connectionText = string.Empty;
    private string _userText = string.Empty;
    private string _notificationText = string.Empty;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ActiveTitle
    {
        get => _activeTitle;
        private set => SetProperty(ref _activeTitle, value);
    }

    public string SizeClassText
    {
        get => _sizeClassText;
        private set => SetProperty(ref _sizeClassText, value);
    }

    public string LanguageText
    {
        get => _languageText;
        private set => SetProperty(ref _languageText, value);
    }

    /// <summary>Connection state (online/offline/signed out) plus "Live" when the event stream is connected.</summary>
    public string ConnectionText
    {
        get => _connectionText;
        private set => SetProperty(ref _connectionText, value);
    }

    /// <summary>Display name of the signed-in user.</summary>
    public string UserText
    {
        get => _userText;
        private set => SetProperty(ref _userText, value);
    }

    /// <summary>Unread notifications ("3 new"; empty when none).</summary>
    public string NotificationText
    {
        get => _notificationText;
        private set => SetProperty(ref _notificationText, value);
    }

    private ClientAppContext Client => Context<ClientAppContext>();

    /// <summary>Shows a startup status in the status line until the next status.</summary>
    public void SetStartupStatus(string? text)
    {
        _startupStatus = text;
        UpdateStatus();
    }

    public void OpenNotifications() => Client.Navigation.NavigateTo(BuiltInViews.Notifications);

    /// <summary>Shows the "waiting for the second key" hint of a chord (null hides it).</summary>
    public void SetChordHint(string? gesture)
    {
        _chordHint = gesture;
        UpdateStatus();
    }

    protected override void OnCreated()
    {
        UpdateTexts();
        AppContext.Languages.LanguageChanged += (_, _) => UpdateTexts();
        Client.Dock.ActiveItemChanged += (_, _) => OnActiveItemChanged();
        Client.Backend.StateChanged += (_, _) => Run(UpdateConnection);
        Client.Backend.SignedIn += (_, _) => Run(UpdateConnection);
        Client.Backend.SignedOut += (_, _) => Run(UpdateConnection);
        Client.Hub.ConnectionChanged += (_, _) => Run(UpdateConnection);
        Client.InApp.Changed += (_, _) => Run(UpdateNotifications);
    }

    private void Run(Action action)
    {
        if (AppContext.Dispatcher is { } dispatcher)
        {
            dispatcher.Run(action);
        }
        else
        {
            action();
        }
    }

    private void UpdateConnection()
    {
        var languages = AppContext.Languages;
        var backend = Client.Backend;
        var state = !backend.IsSignedIn ? "connection.signedout"
            : backend.State == ConnectionState.Offline ? "connection.offline"
            : "connection.online";
        ConnectionText = languages.GetText("shell:" + state) + (backend.IsSignedIn && Client.Hub.IsConnected ? " · " + languages.GetText("shell:connection.live") : string.Empty);
        UserText = backend.CurrentUser?.DisplayName ?? string.Empty;
    }

    private void UpdateNotifications()
    {
        var unread = Client.InApp.UnreadCount;
        NotificationText = unread == 0 ? string.Empty : AppContext.Languages.GetText("shell:notifications.unread", new Dictionary<string, object?> { ["count"] = unread });
    }

    protected override void OnSizeClassChanged() =>
        SizeClassText = AppContext.Languages.GetText($"shell:sizeclass.{SizeClass.ToString().ToLowerInvariant()}");

    private void UpdateTexts()
    {
        UpdateStatus();
        OnSizeClassChanged();
        LanguageText = AppContext.Languages.CurrentInfo.NativeName;
        OnActiveItemChanged();
        UpdateConnection();
        UpdateNotifications();
    }

    private void UpdateStatus() =>
        StatusText = _chordHint is not null
            ? AppContext.Languages.GetText("shell:status.chord", new Dictionary<string, object?> { ["gesture"] = _chordHint })
            : _startupStatus ?? AppContext.Languages.GetText("shell:status.ready");

    private void OnActiveItemChanged()
    {
        var item = Client.Dock.ActiveItem;
        ActiveTitle = item?.Title ?? string.Empty;
        Client.Commands.Shortcuts.ActiveScopes = item is null
            ? []
            : new (ShortcutScope Scope, string? Key)[] { (ShortcutScope.View, item.NavigationKey), (ShortcutScope.Module, item.ModuleId) };
    }
}
