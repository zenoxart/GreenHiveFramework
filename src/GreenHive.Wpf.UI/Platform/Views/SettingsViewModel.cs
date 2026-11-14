using System.Collections.ObjectModel;
using System.Diagnostics;
using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.UI.Platform;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.UI.Platform.Views;

/// <summary>One row of the shortcut table.</summary>
public sealed record ShortcutRow(string CommandId, string Label, string Gesture, string Source, bool IsChanged);

/// <summary>ViewModel of the settings page: language, keyboard shortcuts, data folders.</summary>
public sealed class SettingsViewModel : ViewModelAppObject
{
    private string? _recordingCommandId;

    public ObservableCollection<ShortcutRow> Shortcuts { get; } = [];

    public IReadOnlyList<LanguageInfo> Languages => AppContext.Languages.Supported;

    public LanguageInfo CurrentLanguage => AppContext.Languages.CurrentInfo;

    public string LocalDataPath => AppContext.LocalDataPath;

    public string RoamingDataPath => AppContext.RoamingDataPath;

    public string? RecordingCommandId => _recordingCommandId;

    private ClientAppContext Client => Context<ClientAppContext>();

    public void SetLanguage(LanguageInfo language)
    {
        if (language.Code.Equals(AppContext.Languages.Current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AppContext.Languages.Set(language.Code);
        Client.Settings.Current.Language = language.Code;
        Client.Settings.Save();
    }

    /// <summary>Starts recording a new gesture; the shortcut router is suspended meanwhile.</summary>
    public void StartRecording(string commandId)
    {
        _recordingCommandId = commandId;
        Client.Windows.InputRouter.IsSuspended = true;
        Refresh();
    }

    /// <summary>Applies a recorded gesture (null = cancelled).</summary>
    public void FinishRecording(KeyGesture? gesture)
    {
        var commandId = _recordingCommandId;
        _recordingCommandId = null;
        Client.Windows.InputRouter.IsSuspended = false;
        if (commandId is not null && gesture is not null)
        {
            Client.Commands.Shortcuts.SetUserGesture(commandId, gesture);
            AppContext.Log.Write($"Shortcut of '{commandId}' set to {gesture}", LogEntryType.Information, nameof(SettingsViewModel));
        }

        Refresh();
    }

    public void Reset(string commandId)
    {
        Client.Commands.Shortcuts.ResetUserGesture(commandId);
        Refresh();
    }

    public void ResetAll()
    {
        Client.Commands.Shortcuts.ResetAll();
        Refresh();
    }

    public void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex);
        }
    }

    public void Refresh()
    {
        var pool = Client.Commands;
        var languages = AppContext.Languages;
        Shortcuts.Clear();
        foreach (var registration in pool.Registry.All.OrderBy(r => pool.GetLabel(r.Descriptor.Id), StringComparer.CurrentCultureIgnoreCase))
        {
            var id = registration.Descriptor.Id;
            var gesture = id == _recordingCommandId
                ? languages.GetText("shell:settings.recording")
                : pool.Shortcuts.GetGestureText(id) ?? languages.GetText("shell:settings.none");
            var source = pool.Shortcuts.Effective.FirstOrDefault(e => e.CommandId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Source.ToString() ?? string.Empty;
            Shortcuts.Add(new ShortcutRow(id, pool.GetLabel(id), gesture, source, pool.Shortcuts.HasUserOverride(id)));
        }
    }

    protected override void OnCreated()
    {
        Refresh();
        AppContext.Languages.LanguageChanged += (_, _) =>
        {
            Refresh();
            OnPropertyChanged(nameof(CurrentLanguage));
        };
        Client.Commands.Shortcuts.BindingsChanged += (_, _) => Refresh();
    }
}
