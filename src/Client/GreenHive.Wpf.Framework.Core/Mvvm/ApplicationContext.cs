using System.Collections.Concurrent;
using System.Reflection;

namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// Provides the infrastructure of the application and produces all application objects
/// (WIFI: AppKontext). Named ApplicationContext because "AppContext" would clash with System.AppContext.
/// Derived contexts (ClientAppContext, ModuleAppContext) add services the same way.
/// </summary>
public class ApplicationContext
{
    private readonly object _sync = new();
    private LogManager? _log;
    private LanguageManager? _languages;
    private string? _localDataPath;
    private string? _roamingDataPath;

    /// <summary>True when the app runs with package identity (MSIX); set by the host at startup.</summary>
    public static bool IsPackaged { get; set; }

    /// <summary>The application log (WIFI: Protokoll).</summary>
    public virtual LogManager Log
    {
        get
        {
            if (_log is null)
            {
                lock (_sync)
                {
                    // Created directly: producing it would write a log entry before the log exists.
                    _log ??= new LogManager { AppContext = this };
                }
            }

            return _log;
        }
    }

    /// <summary>The application languages and texts (WIFI: Sprachen).</summary>
    public virtual LanguageManager Languages => GetOrProduce(ref _languages);

    /// <summary>Session cache.</summary>
    public ConcurrentDictionary<string, object> Cache { get; } = new();

    /// <summary>UI dispatcher; owned by the central command manager and set by the host at startup.</summary>
    public IUiDispatcher? Dispatcher { get; set; }

    /// <summary>Company name used in data paths.</summary>
    public virtual string Company => EntryAssembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "GreenHive";

    /// <summary>Product name used in data paths.</summary>
    public virtual string Title => EntryAssembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? EntryAssembly?.GetName().Name ?? "GreenHive";

    /// <summary>Version used in data paths.</summary>
    public virtual string Version => EntryAssembly?.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>Machine-local data folder (%LocalAppData%\Company\Title\Version).</summary>
    public string LocalDataPath => EnsureDirectory(ref _localDataPath, () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SafeName(Company), SafeName(Title), Version));

    /// <summary>User data folder that may roam (%AppData%\Company\Title\Version).</summary>
    public string RoamingDataPath => EnsureDirectory(ref _roamingDataPath, () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), SafeName(Company), SafeName(Title), Version));

    /// <summary>Folder the application was started from.</summary>
    public string ApplicationPath => System.AppContext.BaseDirectory;

    private static Assembly? EntryAssembly => Assembly.GetEntryAssembly();

    /// <summary>
    /// Returns an initialized application object (WIFI: Produziere): sets the context, attaches an
    /// error handler that writes to the log, logs the production and calls <see cref="AppObject.OnCreated"/>.
    /// </summary>
    public virtual T Produce<T>()
        where T : IAppContext, new()
    {
        var produced = new T();
        produced.AppContext = this;

        if (produced is AppObject appObject && produced is not LogManager)
        {
            appObject.ErrorOccurred += (sender, e) =>
                Log.Write($"{sender} reported an error: {e.Cause.Message}", LogEntryType.Error, sender?.ToString(), e.Cause);
        }

        if (produced is not LogManager)
        {
            Log.Write($"{produced} was produced", LogEntryType.NewInstance);
        }

        OnProduced(produced);
        (produced as AppObject)?.NotifyCreated();
        return produced;
    }

    /// <summary>Hook for derived contexts: further production steps.</summary>
    protected virtual void OnProduced(object produced)
    {
    }

    /// <summary>Thread-safe lazy creation of a service field via <see cref="Produce{T}"/>.</summary>
    protected T GetOrProduce<T>(ref T? field)
        where T : class, IAppContext, new()
    {
        if (field is null)
        {
            lock (_sync)
            {
                field ??= Produce<T>();
            }
        }

        return field;
    }

    private static string SafeName(string name)
    {
        // WIFI fix of 2021-01-28: characters such as "/" and "&" in company or title broke the path.
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) || c == '&' ? '_' : c).ToArray());
    }

    private string EnsureDirectory(ref string? field, Func<string> factory)
    {
        if (field is null)
        {
            lock (_sync)
            {
                field ??= factory();
            }
        }

        Directory.CreateDirectory(field);
        return field;
    }
}
