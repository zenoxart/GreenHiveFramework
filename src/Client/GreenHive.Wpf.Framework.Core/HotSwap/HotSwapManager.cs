using System.Text.Json.Nodes;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.Wpf.Framework.ModuleLoader;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.NotificationClient;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.Wpf.Framework.HotSwap;

/// <summary>Raised before a module is swapped (the WorkflowManager pauses client-side runs of that module).</summary>
public sealed record ModuleSwapEventArgs(string ModuleId, string FromVersion, string ToVersion);

public enum HotSwapPolicy
{
    /// <summary>Swap as soon as the module is quiet.</summary>
    Automatic,

    /// <summary>Show a notification; the user starts the swap (ApplyPendingAsync).</summary>
    Ask,
}

/// <summary>
/// Reacts to version assignments of the Update Orchestrator: fetches the assignments, swaps modules whose hotSwap mode
/// allows it (quiescence first), and reports modules that need a restart.
/// </summary>
public sealed class HotSwapManager : AppObject
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<IDisposable> _subscriptions = [];
    private Timer? _debounce;

    public event EventHandler<ModuleSwapEventArgs>? ModuleSwapping;

    public event EventHandler<ModuleSwapEventArgs>? ModuleSwapped;

    public event EventHandler? PendingChanged;

    public ModuleQuiescence Quiescence { get; } = new();

    public UnloadMonitor Monitor { get; } = new();

    public HotSwapPolicy Policy { get; set; } = HotSwapPolicy.Automatic;

    public TimeSpan Grace { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Modules with a newer assigned version that are not swapped yet (Ask policy or restart required).</summary>
    public IReadOnlyList<ModuleSwapEventArgs> Pending { get; private set; } = [];

    /// <summary>Modules whose new version only takes effect after a restart.</summary>
    public IReadOnlyList<ModuleSwapEventArgs> RestartRequired { get; private set; } = [];

    private IModuleHost Host => (IModuleHost)AppContext;

    /// <summary>Connects to the notification router and installs the command gate.</summary>
    public void Start()
    {
        Host.Commands.Executor.Gate = Quiescence;
        var router = Host.Router;
        _subscriptions.Add(router.Subscribe(EventTypes.ModuleVersionAvailable, OnVersionEvent));
        _subscriptions.Add(router.Subscribe(EventTypes.ModuleUpdateStatus, OnVersionEvent));
        _subscriptions.Add(router.Subscribe(EventTypes.ModulePublished, OnVersionEvent));
    }

    public void Stop()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        _debounce?.Dispose();
    }

    private void OnVersionEvent(NotificationEnvelope envelope)
    {
        var moduleId = (envelope.Payload as JsonObject)?["moduleId"]?.GetValue<string>();
        AppContext.Log.Write($"Update notice {envelope.Type} for {moduleId ?? "?"}", LogEntryType.Information, nameof(HotSwapManager));

        // Several notices arrive together (assignment + status); check once after they settle.
        _debounce?.Dispose();
        _debounce = new Timer(state => AppContext.Dispatcher?.Run(() => _ = CheckAsync()), null, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
    }

    /// <summary>Compares the assigned versions with the loaded ones and swaps or records what changed.</summary>
    public async Task CheckAsync(CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct))
        {
            return;
        }

        try
        {
            var modules = Host.Modules;
            var catalog = await modules.CatalogClient.CatalogAsync(modules.HostInfo, ct);
            if (!catalog.TryGetValue(out var entries, out var error))
            {
                AppContext.Log.Write($"Update check failed: {error.Code}", LogEntryType.Warning, nameof(HotSwapManager));
                return;
            }

            var pending = new List<ModuleSwapEventArgs>();
            var restart = new List<ModuleSwapEventArgs>();
            foreach (var entry in entries.Where(e => e.Compatible))
            {
                var id = entry.Manifest.ModuleId;
                var loaded = modules.Find(id);
                if (loaded is null || loaded.Manifest.Version == entry.Manifest.Version)
                {
                    continue;
                }

                var swap = new ModuleSwapEventArgs(id, loaded.Manifest.Version, entry.Manifest.Version);
                if (entry.Manifest.Client?.HotSwap == ModuleHotSwap.RestartRequired || loaded.Manifest.Client?.HotSwap == ModuleHotSwap.RestartRequired)
                {
                    restart.Add(swap);
                    await modules.AcquireAsync(entry, ct: ct);
                    continue;
                }

                if (Policy == HotSwapPolicy.Ask)
                {
                    pending.Add(swap);
                    continue;
                }

                await SwapAsync(entry, swap, ct);
            }

            Pending = pending;
            RestartRequired = restart;
            if (pending.Count > 0)
            {
                Host.InApp.Show("updates:available.title", "updates:available.message", NotificationSeverity.Info);
            }

            foreach (var item in restart)
            {
                Host.InApp.Show("updates:restart.title", $"{item.ModuleId} {item.ToVersion}", NotificationSeverity.Warning);
            }

            PendingChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Swaps all pending modules (Ask policy).</summary>
    public async Task ApplyPendingAsync(CancellationToken ct = default)
    {
        var policy = Policy;
        Policy = HotSwapPolicy.Automatic;
        try
        {
            await CheckAsync(ct);
        }
        finally
        {
            Policy = policy;
        }
    }

    private async Task SwapAsync(ModuleCatalogEntry entry, ModuleSwapEventArgs swap, CancellationToken ct)
    {
        var modules = Host.Modules;
        var acquired = await modules.AcquireAsync(entry, ct: ct);
        if (!acquired.TryGetValue(out var package, out var error))
        {
            Host.InApp.Show("updates:failed.title", $"{swap.ModuleId} {swap.ToVersion}: {error.Message}", NotificationSeverity.Error);
            return;
        }

        var old = modules.Find(swap.ModuleId);
        if (old is not null && old.Manifest.Version == package.Manifest.Version)
        {
            // No package of the assigned version yet (e.g. only the development build exists) - nothing to swap.
            AppContext.Log.Write($"Module {swap.ModuleId}: {swap.ToVersion} assigned but no package available yet", LogEntryType.Information, nameof(HotSwapManager));
            return;
        }

        ModuleSwapping?.Invoke(this, swap);
        using (await Quiescence.QuiesceAsync(swap.ModuleId, Grace, ct))
        {
            var result = await modules.ReplaceAsync(package, ct);
            if (result.IsFailure)
            {
                // Roll back to the previous version if the new one cannot be loaded.
                if (old is not null && modules.Find(swap.ModuleId) is null)
                {
                    modules.Load(old.Local);
                }

                Host.InApp.Show("updates:failed.title", $"{swap.ModuleId} {swap.ToVersion}: {result.Error.Message}", NotificationSeverity.Error);
                return;
            }
        }

        if (old is not null)
        {
            Monitor.Track(old);
        }

        AppContext.Log.Write($"Module {swap.ModuleId} swapped {swap.FromVersion} -> {swap.ToVersion}", LogEntryType.Information, nameof(HotSwapManager));
        Host.InApp.Show("updates:swapped.title", $"{swap.ModuleId} {swap.ToVersion}", NotificationSeverity.Success);
        ModuleSwapped?.Invoke(this, swap);
    }
}
