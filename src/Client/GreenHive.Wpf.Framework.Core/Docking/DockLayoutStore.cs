using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Docking;

/// <summary>Persists the dock layout in AppData: LocalDataPath\Layout\DockLayout.json.</summary>
public sealed class DockLayoutStore : AppObject
{
    private JsonController<DockLayoutState>? _json;

    public string FilePath => Path.Combine(AppContext.LocalDataPath, "Layout", "DockLayout.json");

    private JsonController<DockLayoutState> Json => _json ??= AppContext.Produce<JsonController<DockLayoutState>>();

    public DockLayoutState? Load() => Json.TryLoad(FilePath);

    public void Save(DockLayoutState state) => Json.TrySave(state, FilePath);

    public void Delete()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (IOException)
        {
        }
    }
}
