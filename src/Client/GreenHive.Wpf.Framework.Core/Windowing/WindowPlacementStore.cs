using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Windowing;

/// <summary>
/// Stores window placements in AppData: LocalDataPath\Layout\WindowLayout.json
/// (local, not roaming: monitor setups differ per machine). Saved debounced after moves and on close.
/// </summary>
public sealed class WindowPlacementStore : AppObject
{
    private readonly object _sync = new();
    private WindowLayoutFile? _data;
    private Timer? _saveTimer;
    private JsonController<WindowLayoutFile>? _json;

    public string FilePath => Path.Combine(AppContext.LocalDataPath, "Layout", "WindowLayout.json");

    private JsonController<WindowLayoutFile> Json => _json ??= AppContext.Produce<JsonController<WindowLayoutFile>>();

    public WindowPlacement? Get(string key)
    {
        lock (_sync)
        {
            return Data.Windows.TryGetValue(key, out var placement) ? placement : null;
        }
    }

    public void Set(WindowPlacement placement)
    {
        lock (_sync)
        {
            Data.Windows[placement.Key] = placement;
        }

        SaveSoon();
    }

    /// <summary>Saves immediately (e.g. on app close).</summary>
    public void Save()
    {
        WindowLayoutFile copy;
        lock (_sync)
        {
            copy = new WindowLayoutFile { Windows = new Dictionary<string, WindowPlacement>(Data.Windows, StringComparer.OrdinalIgnoreCase) };
        }

        Json.TrySave(copy, FilePath);
    }

    private WindowLayoutFile Data => _data ??= Json.TryLoad(FilePath) ?? new WindowLayoutFile();

    private void SaveSoon()
    {
        lock (_sync)
        {
            _saveTimer ??= new Timer(_ => Save());
            _saveTimer.Change(TimeSpan.FromMilliseconds(600), Timeout.InfiniteTimeSpan);
        }
    }
}
