using GreenHive.Wpf.Framework.ModuleLoader;

namespace GreenHive.Wpf.Framework.HotSwap;

/// <summary>A replaced module version that may still be in memory.</summary>
public sealed record RetiredModule(string ModuleId, string Version, DateTimeOffset RetiredAt, WeakReference Context);

/// <summary>
/// Keeps weak references to replaced module contexts so the host can show how many old versions are still alive.
/// Load contexts are not collectible (WPF types cannot be unloaded reliably), so a restart releases them for good.
/// </summary>
public sealed class UnloadMonitor
{
    private readonly List<RetiredModule> _retired = [];

    public void Track(LoadedModule module)
    {
        lock (_retired)
        {
            _retired.Add(new RetiredModule(module.Manifest.ModuleId, module.Manifest.Version, DateTimeOffset.Now, new WeakReference(module.Context)));
        }
    }

    /// <summary>Old versions whose module context is still referenced.</summary>
    public IReadOnlyList<RetiredModule> Alive()
    {
        lock (_retired)
        {
            _retired.RemoveAll(r => !r.Context.IsAlive);
            return _retired.ToList();
        }
    }

    /// <summary>True when replaced versions are piling up and a restart is recommended.</summary>
    public bool RestartRecommended => Alive().Count >= 5;
}
