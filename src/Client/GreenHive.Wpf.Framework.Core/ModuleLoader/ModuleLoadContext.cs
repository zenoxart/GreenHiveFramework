using System.Reflection;
using System.Runtime.Loader;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>
/// Load context of one module. Framework assemblies (contracts, MVVM, WPF, the module SDK) are shared with the host's
/// default context - a module never brings its own copy; everything else is loaded from the module's folder.
/// Not collectible: WPF keeps static references to dependency properties, styles and routed events of loaded types, so an updated module is loaded into a new
/// context and the old one stays in memory until the app restarts (see UnloadMonitor).
/// </summary>
public sealed class ModuleLoadContext(string moduleId, string folder) : AssemblyLoadContext($"module:{moduleId}:{Guid.NewGuid():n}", isCollectible: false)
{
    public string Folder { get; } = folder;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (Default.Assemblies.Any(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase)) ||
            File.Exists(Path.Combine(System.AppContext.BaseDirectory, assemblyName.Name + ".dll")))
        {
            return null; // shared with the host
        }

        var local = Path.Combine(Folder, assemblyName.Name + ".dll");
        return File.Exists(local) ? LoadFromAssemblyPath(local) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var local = Path.Combine(Folder, unmanagedDllName);
        return File.Exists(local) ? LoadUnmanagedDllFromPath(local) : IntPtr.Zero;
    }
}
