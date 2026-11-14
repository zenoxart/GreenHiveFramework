using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>Persists the user's shortcut overrides in RoamingDataPath\Shortcuts.json (command ID -> gestures).</summary>
public sealed class ShortcutProfileStore : AppObject
{
    private JsonController<Dictionary<string, List<string>>>? _json;

    public string FilePath => Path.Combine(AppContext.RoamingDataPath, "Shortcuts.json");

    private JsonController<Dictionary<string, List<string>>> Json => _json ??= AppContext.Produce<JsonController<Dictionary<string, List<string>>>>();

    public Dictionary<string, List<string>> Load() =>
        Json.TryLoad(FilePath) ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    public void Save(Dictionary<string, List<string>> overrides) => Json.TrySave(overrides, FilePath);
}
