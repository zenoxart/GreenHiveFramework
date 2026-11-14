using System.IO;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>User settings of the host.</summary>
public sealed class HostSettings
{
    public string? Language { get; set; }
}

/// <summary>Stores the host settings in RoamingDataPath\Settings.json.</summary>
public sealed class HostSettingsStore : AppObject
{
    private HostSettings? _current;
    private JsonController<HostSettings>? _json;

    public string FilePath => Path.Combine(AppContext.RoamingDataPath, "Settings.json");

    public HostSettings Current => _current ??= Json.TryLoad(FilePath) ?? new HostSettings();

    private JsonController<HostSettings> Json => _json ??= AppContext.Produce<JsonController<HostSettings>>();

    public void Save() => Json.TrySave(Current, FilePath);
}
