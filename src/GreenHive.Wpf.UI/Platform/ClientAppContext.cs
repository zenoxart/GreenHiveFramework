using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Docking;
using GreenHive.Wpf.Framework.HotSwap;
using GreenHive.Wpf.Framework.ModuleLoader;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Navigation;
using GreenHive.Wpf.Framework.NotificationClient;
using GreenHive.Wpf.Framework.Security;
using GreenHive.Wpf.Framework.Windowing;
using GreenHive.Wpf.Framework.Workflows;
using GreenHive.Wpf.Framework.XamlMetadata;
using ModuleLoaderService = GreenHive.Wpf.Framework.ModuleLoader.ModuleLoader;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>
/// The application context of the WPF client (WIFI: DatenbankAppKontext). Adds every framework service
/// as a lazily produced property; it is the composition root of the host and the IModuleHost the modules see
/// (each module gets a ModuleAppContext that forwards to it).
/// </summary>
public sealed class ClientAppContext : ApplicationContext, IModuleHost, IWindowContext, IWorkflowContext, ITokenStoreContext
{
    private RelayCommandPool? _commands;
    private IconCatalog? _icons;
    private WindowManager? _windows;
    private DockManager? _dock;
    private NavigationService? _navigation;
    private HostSettingsStore? _settings;
    private BackendConnection? _backend;
    private UserProfileService? _profile;
    private NotificationHubClient? _hub;
    private NotificationRouter? _router;
    private InAppNotificationService? _inApp;
    private AppNotificationService? _toasts;
    private PushNotificationService? _push;
    private ModuleLoaderService? _modules;
    private WorkflowManager? _workflows;
    private HotSwapManager? _hotSwap;
    private ProtectedTokenStore? _tokens;

    public override string Company => "ZenoxArt";

    public override string Title => "Green Hive";

    /// <summary>The central command manager (incl. dispatcher and shortcut manager).</summary>
    public RelayCommandPool Commands => GetOrProduce(ref _commands);

    public IconCatalog Icons => GetOrProduce(ref _icons);

    public WindowManager Windows => GetOrProduce(ref _windows);

    public DockManager Dock => GetOrProduce(ref _dock);

    IDockService IDockContext.Dock => Dock;

    /// <summary>Remembered sessions, encrypted per Windows user (DPAPI).</summary>
    public ITokenStore Tokens => GetOrProduce(ref _tokens);

    public NavigationService Navigation => GetOrProduce(ref _navigation);

    public HostSettingsStore Settings => GetOrProduce(ref _settings);

    /// <summary>Connection to the Green Hive gateway (sign-in, tokens, permissions, HTTP).</summary>
    public BackendConnection Backend => GetOrProduce(ref _backend);

    /// <summary>Profile of the signed-in user (picture, password).</summary>
    public UserProfileService Profile => GetOrProduce(ref _profile);

    /// <summary>Server-sent event stream of the NotificationPoint.</summary>
    public NotificationHubClient Hub => GetOrProduce(ref _hub);

    public NotificationRouter Router => GetOrProduce(ref _router);

    /// <summary>In-app notification centre (also receives every routed event).</summary>
    public InAppNotificationService InApp => GetOrProduce(ref _inApp);

    /// <summary>Windows toasts while the app is in the background.</summary>
    public AppNotificationService Toasts => GetOrProduce(ref _toasts);

    /// <summary>Optional WNS push channel (only used when the backend has WNS configured).</summary>
    public PushNotificationService Push => GetOrProduce(ref _push);

    public ModuleLoaderService Modules => GetOrProduce(ref _modules);

    public WorkflowManager Workflows => GetOrProduce(ref _workflows);

    public HotSwapManager HotSwap => GetOrProduce(ref _hotSwap);

    /// <summary>Modules register their workflow step types and templates with the workflow manager.</summary>
    public IWorkflowRegistrar? WorkflowRegistrar => Workflows;
}
