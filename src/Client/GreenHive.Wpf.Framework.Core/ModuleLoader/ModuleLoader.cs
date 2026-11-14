using System.Collections.ObjectModel;
using System.Reflection;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>A module running in the client.</summary>
public sealed class LoadedModule(LocalModule local, ModuleLoadContext loadContext, IModuleEntryPoint entryPoint, ModuleAppContext context)
{
    public LocalModule Local { get; } = local;

    public ModuleManifest Manifest => Local.Manifest;

    public ModuleLoadContext LoadContext { get; } = loadContext;

    public IModuleEntryPoint EntryPoint { get; } = entryPoint;

    public ModuleAppContext Context { get; } = context;

    public DateTimeOffset LoadedAt { get; } = DateTimeOffset.Now;
}

/// <summary>
/// Loads, unloads and replaces client modules.
/// <para>Which version: the catalogue of the Module Registry (the rollout decides per user); the package is taken from the
/// local store, downloaded from the ModuleDelivery service, or - during development - taken from the development folder
/// next to the host. Without a connection the development and already installed modules are loaded.</para>
/// </summary>
public sealed class ModuleLoader : AppObject
{
    private ModuleStore? _store;
    private ModulePackageDownloader? _downloader;
    private ModuleCatalogClient? _catalog;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event EventHandler? Changed;

    public ObservableCollection<LoadedModule> Loaded { get; } = [];

    public IReadOnlyList<ModuleCatalogEntry> Catalog { get; private set; } = [];

    public IReadOnlyList<(string ModuleId, string Reason)> Problems { get; private set; } = [];

    public ModuleStore Store => _store ??= AppContext.Produce<ModuleStore>();

    public ModuleCatalogClient CatalogClient => _catalog ??= AppContext.Produce<ModuleCatalogClient>();

    private ModulePackageDownloader Downloader => _downloader ??= AppContext.Produce<ModulePackageDownloader>();

    private IModuleHost Host => Context<IModuleHost>();

    /// <summary>Facts about this host for the compatibility rules.</summary>
    public HostInfo HostInfo => new(AppContext.Version, DesktopRuntimeVersion());

    public LoadedModule? Find(string moduleId) => Loaded.FirstOrDefault(m => m.Manifest.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase));

    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var problems = new List<(string, string)>();
            var local = Store.Discover();
            var wanted = new Dictionary<string, LocalModule>(StringComparer.OrdinalIgnoreCase);

            if (Host.Backend.IsSignedIn)
            {
                var catalog = await CatalogClient.CatalogAsync(HostInfo, ct);
                if (catalog.TryGetValue(out var entries, out var error))
                {
                    Catalog = entries;
                    foreach (var entry in entries)
                    {
                        if (!entry.Compatible)
                        {
                            problems.Add((entry.Manifest.ModuleId, string.Join("; ", entry.Problems)));
                            continue;
                        }

                        var acquired = await AcquireAsync(entry, local, ct);
                        if (acquired.TryGetValue(out var module, out var acquireError))
                        {
                            wanted[entry.Manifest.ModuleId] = module;
                        }
                        else
                        {
                            problems.Add((entry.Manifest.ModuleId, acquireError.Message));
                        }
                    }
                }
                else
                {
                    AppContext.Log.Write($"Module catalogue not available ({error.Code}) - loading local modules", LogEntryType.Warning, nameof(ModuleLoader));
                }
            }

            // Development and installed modules that the catalogue did not provide (offline or not published yet).
            foreach (var group in local.GroupBy(m => m.Manifest.ModuleId, StringComparer.OrdinalIgnoreCase))
            {
                if (!wanted.ContainsKey(group.Key) && !Catalog.Any(c => c.Manifest.ModuleId.Equals(group.Key, StringComparison.OrdinalIgnoreCase) && !c.Compatible))
                {
                    wanted[group.Key] = group.OrderByDescending(m => m.Source == ModuleSource.Development).ThenByDescending(m => m.Manifest.SemanticVersion).First();
                }
            }

            var plan = ModuleDependencyResolver.Resolve(wanted.Values.ToList(), HostInfo);
            problems.AddRange(plan.Skipped.Select(s => (s.Module.Manifest.ModuleId, s.Reason)));
            foreach (var module in plan.Load)
            {
                if (Find(module.Manifest.ModuleId) is { } loaded && loaded.Manifest.Version == module.Manifest.Version)
                {
                    continue;
                }

                var result = Find(module.Manifest.ModuleId) is not null ? await ReplaceCoreAsync(module, ct) : Load(module);
                if (result.IsFailure)
                {
                    problems.Add((module.Manifest.ModuleId, result.Error.Message));
                }
            }

            Problems = problems;
            foreach (var (moduleId, reason) in problems)
            {
                AppContext.Log.Write($"Module {moduleId} not loaded: {reason}", LogEntryType.Warning, nameof(ModuleLoader));
            }
        }
        finally
        {
            _lock.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the package of a catalogue version: installed, development build of the same version, download, or development fallback.</summary>
    public async Task<Result<LocalModule>> AcquireAsync(ModuleCatalogEntry entry, IReadOnlyList<LocalModule>? local = null, CancellationToken ct = default)
    {
        local ??= Store.Discover();
        var id = entry.Manifest.ModuleId;
        var version = entry.Manifest.Version;
        var exact = local.FirstOrDefault(m => m.Manifest.ModuleId.Equals(id, StringComparison.OrdinalIgnoreCase) && m.Manifest.Version == version);
        if (exact is not null)
        {
            return exact;
        }

        if (entry.PackageUrl is not null)
        {
            var downloaded = await Downloader.DownloadAsync(entry.Manifest, ct);
            if (downloaded.IsSuccess)
            {
                return downloaded;
            }

            AppContext.Log.Write($"Package {id} {version} not available: {downloaded.Error.Code}", LogEntryType.Information, nameof(ModuleLoader));
        }

        var development = local.Where(m => m.Manifest.ModuleId.Equals(id, StringComparison.OrdinalIgnoreCase) && m.Source == ModuleSource.Development)
            .OrderByDescending(m => m.Manifest.SemanticVersion).FirstOrDefault();
        if (development is not null)
        {
            AppContext.Log.Write($"Module {id}: using the development build {development.Manifest.Version} (registry assigns {version})",
                LogEntryType.Information, nameof(ModuleLoader));
            return development;
        }

        return Error.NotFound("modules.package.missing", "modules:package.notFound").WithMetadata("module", $"{id}@{version}");
    }

    /// <summary>Loads one module into its own load context and initializes it.</summary>
    public Result<LoadedModule> Load(LocalModule module)
    {
        var manifest = module.Manifest;
        if (manifest.Client is null)
        {
            return Error.Validation("modules.client.missing", "modules:manifest.noClient");
        }

        var loadContext = new ModuleLoadContext(manifest.ModuleId, module.Folder);
        ModuleAppContext? context = null;
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(module.EntryAssemblyPath);
            var type = assembly.GetType(manifest.Client.EntryType, throwOnError: true)!;
            if (Activator.CreateInstance(type) is not IModuleEntryPoint entryPoint)
            {
                return Error.Validation("modules.entry.type", $"{manifest.Client.EntryType} does not implement IModuleEntryPoint.");
            }

            context = new ModuleAppContext(Host, manifest, module.Folder);
            entryPoint.Initialize(context);
            var loaded = new LoadedModule(module, loadContext, entryPoint, context);
            AppContext.Dispatcher?.Run(() => Loaded.Add(loaded));
            AppContext.Log.Write($"Module {manifest.ModuleId} {manifest.Version} loaded ({module.Source})", LogEntryType.Information, nameof(ModuleLoader));
            return loaded;
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException or TypeLoadException or
                                       TargetInvocationException or InvalidOperationException or ArgumentException or MissingMethodException)
        {
            context?.RemoveRegistrations();
            AppContext.Log.Write($"Module {manifest.ModuleId} could not be loaded: {ex.Message}", LogEntryType.Error, nameof(ModuleLoader), ex);
            return Error.Unexpected("modules.load", ex.Message, ex);
        }
    }

    /// <summary>Closes the module's views, shuts it down and removes all its registrations.</summary>
    public async Task<Result<IReadOnlyList<(string Key, string? Parameter)>>> UnloadAsync(string moduleId)
    {
        if (Find(moduleId) is not { } loaded)
        {
            return Error.NotFound("modules.notLoaded", "modules:notLoaded");
        }

        var open = Host.Dock.OpenItems.Where(i => i.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase)).ToList();
        var reopen = open.Select(i => (i.NavigationKey, i.Parameter)).ToList();
        foreach (var item in open)
        {
            Host.Dock.Close(item);
        }

        try
        {
            await loaded.EntryPoint.ShutdownAsync();
        }
        catch (Exception ex)
        {
            AppContext.Log.Write($"Module {moduleId} shutdown failed: {ex.Message}", LogEntryType.Warning, nameof(ModuleLoader), ex);
        }

        loaded.Context.RemoveRegistrations();
        Loaded.Remove(loaded);
        AppContext.Log.Write($"Module {moduleId} {loaded.Manifest.Version} unloaded", LogEntryType.Information, nameof(ModuleLoader));
        Changed?.Invoke(this, EventArgs.Empty);
        return reopen;
    }

    /// <summary>Replaces a running module by another version and reopens its views.</summary>
    public async Task<Result<LoadedModule>> ReplaceAsync(LocalModule module, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await ReplaceCoreAsync(module, ct);
        }
        finally
        {
            _lock.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<Result<LoadedModule>> ReplaceCoreAsync(LocalModule module, CancellationToken ct)
    {
        var unloaded = await UnloadAsync(module.Manifest.ModuleId);
        var reopen = unloaded.GetValueOrDefault([]);
        var loaded = Load(module);
        if (loaded.IsSuccess)
        {
            foreach (var (key, parameter) in reopen)
            {
                if (Host.Navigation.Registry.Find(key) is not null)
                {
                    Host.Navigation.NavigateTo(key, parameter);
                }
            }

            Store.Prune(module.Manifest.ModuleId, new string[] { module.Manifest.Version });
        }

        return loaded;
    }

    /// <summary>Version of the .NET runtime the host runs on (the WPF Desktop Runtime ships with the same version).</summary>
    private static string DesktopRuntimeVersion() => $"{Environment.Version.Major}.{Environment.Version.Minor}.{Environment.Version.Build}";
}
