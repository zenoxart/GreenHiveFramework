using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>Most-recently-used order of modules for the navigation wheel; stored in LocalDataPath\Layout\History.json.</summary>
public sealed class ModuleActivationHistory : AppObject
{
    private List<string>? _modules;
    private JsonController<List<string>>? _json;

    public string FilePath => Path.Combine(AppContext.LocalDataPath, "Layout", "History.json");

    private JsonController<List<string>> Json => _json ??= AppContext.Produce<JsonController<List<string>>>();

    private List<string> Modules => _modules ??= Json.TryLoad(FilePath) ?? [];

    /// <summary>Moves the module to the front.</summary>
    public void Record(string moduleId)
    {
        if (Modules.Count > 0 && Modules[0].Equals(moduleId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Modules.RemoveAll(m => m.Equals(moduleId, StringComparison.OrdinalIgnoreCase));
        Modules.Insert(0, moduleId);
        if (Modules.Count > 50)
        {
            Modules.RemoveRange(50, Modules.Count - 50);
        }

        Json.TrySave(Modules, FilePath);
    }

    /// <summary>Position in the history (0 = most recent); unknown modules come last.</summary>
    public int Rank(string moduleId)
    {
        var index = Modules.FindIndex(m => m.Equals(moduleId, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }
}
