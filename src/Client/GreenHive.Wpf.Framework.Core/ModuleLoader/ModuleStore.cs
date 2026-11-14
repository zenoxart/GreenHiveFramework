using GreenHive.NET.Contracts;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.ModuleLoader;

public enum ModuleSource
{
    /// <summary>Built with the solution and copied next to the host (Modules/&lt;id&gt;/).</summary>
    Development,

    /// <summary>Downloaded from the ModuleDelivery service (AppData/Modules/&lt;id&gt;/&lt;version&gt;/).</summary>
    Installed,
}

/// <summary>A module available on this device.</summary>
public sealed record LocalModule(ModuleManifest Manifest, string Folder, ModuleSource Source)
{
    public string EntryAssemblyPath => Path.Combine(Folder, Manifest.Client?.EntryAssembly ?? string.Empty);
}

/// <summary>Modules on this device: the development folder next to the host and the installed packages in AppData.</summary>
public sealed class ModuleStore : AppObject
{
    public string InstalledRoot => Directory.CreateDirectory(Path.Combine(AppContext.LocalDataPath, "Modules")).FullName;

    public string DevelopmentRoot => Path.Combine(System.AppContext.BaseDirectory, "Modules");

    public string InstalledFolder(string moduleId, string version) => Path.Combine(InstalledRoot, moduleId, version);

    public IReadOnlyList<LocalModule> Discover()
    {
        var modules = new List<LocalModule>();
        if (Directory.Exists(DevelopmentRoot))
        {
            modules.AddRange(Directory.EnumerateDirectories(DevelopmentRoot).Select(d => Read(d, ModuleSource.Development)).OfType<LocalModule>());
        }

        foreach (var module in Directory.EnumerateDirectories(InstalledRoot))
        {
            modules.AddRange(Directory.EnumerateDirectories(module).Select(d => Read(d, ModuleSource.Installed)).OfType<LocalModule>());
        }

        return modules;
    }

    public LocalModule? Find(string moduleId, string version) =>
        Discover().FirstOrDefault(m => m.Manifest.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase) && m.Manifest.Version == version);

    /// <summary>Removes installed versions other than the ones in use (keeps the previous one for rollbacks).</summary>
    public void Prune(string moduleId, IReadOnlyCollection<string> keep)
    {
        var folder = Path.Combine(InstalledRoot, moduleId);
        if (!Directory.Exists(folder))
        {
            return;
        }

        var versions = Directory.EnumerateDirectories(folder)
            .Select(d => (Dir: d, Version: SemanticVersion.TryParse(Path.GetFileName(d), out var v) ? v : SemanticVersion.Zero))
            .OrderByDescending(x => x.Version).ToList();
        foreach (var (dir, version) in versions.Skip(2).Where(x => !keep.Contains(x.Version.ToString())))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // still loaded (non-collectible load context) - removed after the next restart
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private LocalModule? Read(string folder, ModuleSource source)
    {
        var file = Path.Combine(folder, "manifest.json");
        if (!File.Exists(file))
        {
            return null;
        }

        return ModuleManifest.Parse(File.ReadAllText(file)).Match(
            manifest => manifest.Client is null || !File.Exists(Path.Combine(folder, manifest.Client.EntryAssembly)) ? null : new LocalModule(manifest, folder, source),
            error =>
            {
                AppContext.Log.Write($"Invalid module manifest in '{folder}': {error}", LogEntryType.Warning, nameof(ModuleStore));
                return null;
            });
    }
}
