using System.Globalization;
using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Docking;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Navigation;
using GreenHive.Wpf.Framework.NotificationClient;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Security;
using GreenHive.NET.WorkflowContracts;
using GreenHive.Wpf.Framework.XamlMetadata;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>
/// The application context of one module (WIFI: AppKontext of the module) and its IModuleContext.
/// ViewModels of the module are produced here (Produce&lt;T&gt;), share the host's log, languages and services, and every
/// registration is tracked so <see cref="RemoveRegistrations"/> leaves no trace of the module behind.
/// </summary>
public sealed class ModuleAppContext : ApplicationContext, IModuleContext, IModuleHost
{
    private readonly IModuleHost _host;
    private readonly List<IDisposable> _subscriptions = [];
    private IModuleApi? _api;

    public ModuleAppContext(IModuleHost host, ModuleManifest manifest, string folder)
    {
        _host = host;
        Manifest = manifest;
        Folder = folder;
        Dispatcher = ((ApplicationContext)host).Dispatcher;
        host.Backend.StateChanged += OnConnectionChanged;
    }

    public event EventHandler? ConnectionChanged;

    public ModuleManifest Manifest { get; }

    public string Folder { get; }

    public string ModuleId => Manifest.ModuleId;

    public override string Version => Manifest.Version;

    /// <summary>Prefix of the module's TextKeys, commands and icons ("documents").</summary>
    public string Prefix => string.IsNullOrEmpty(Manifest.Prefix) ? Manifest.ModuleId : Manifest.Prefix;

    public string DataPath => Directory.CreateDirectory(Path.Combine(((ApplicationContext)_host).LocalDataPath, "ModuleData", ModuleId)).FullName;

    public override LogManager Log => ((ApplicationContext)_host).Log;

    public override LanguageManager Languages => ((ApplicationContext)_host).Languages;

    public IModuleApi Api => _api ??= Backend.CreateApi(Manifest.Backend?.Route ?? $"/api/{Prefix}");

    public ModuleUser CurrentUser => Backend.ModuleUser;

    public bool IsConnected => Backend.State == ConnectionState.Online;

    // ---- host services (modules and their ViewModels reach them through this context)

    public RelayCommandPool Commands => _host.Commands;

    public IconCatalog Icons => _host.Icons;

    public NavigationService Navigation => _host.Navigation;

    public IDockService Dock => _host.Dock;

    public BackendConnection Backend => _host.Backend;

    public UserProfileService Profile => _host.Profile;

    public NotificationHubClient Hub => _host.Hub;

    public NotificationRouter Router => _host.Router;

    public InAppNotificationService InApp => _host.InApp;

    public ModuleLoader Modules => _host.Modules;

    public IWorkflowRegistrar? WorkflowRegistrar => _host.WorkflowRegistrar;

    // ---- registrations

    public void RegisterCommand(CommandDescriptor descriptor, ICommandHandler handler)
    {
        if (!descriptor.Id.StartsWith(Prefix + ".", StringComparison.OrdinalIgnoreCase))
        {
            Log.Write($"Module {ModuleId}: command '{descriptor.Id}' should start with '{Prefix}.'", LogEntryType.Warning, ModuleId);
        }

        Commands.Register(ModuleId, descriptor, handler);
    }

    public void RegisterGroup(CommandGroupDescriptor group) => Commands.RegisterGroup(ModuleId, group);

    public void RegisterView(ModuleViewRegistration view) =>
        Navigation.Registry.Register(new NavigationTarget(view.Key, ModuleId)
        {
            TextKey = view.TextKey,
            Icon = view.Icon,
            View = view.View,
            IsModuleHome = view.IsModuleHome,
            Group = view.Group ?? "modules",
            Order = view.Order,
            ShowInViewMenu = view.ShowInViewMenu,
            Permission = view.Permission,
            CreateViewModel = _ => (ViewModelAppObject)view.CreateViewModel(),
            CreateView = vm => view.CreateView(vm),
        });

    public void RegisterIcon(string name, string source)
    {
        var fullName = name.StartsWith(Prefix + ".", StringComparison.OrdinalIgnoreCase) ? name : $"{Prefix}.{name}";
        if (source.StartsWith("image:", StringComparison.OrdinalIgnoreCase))
        {
            Icons.RegisterImage(fullName, new Uri(Path.Combine(Folder, source[6..])));
        }
        else
        {
            var hex = source.StartsWith("glyph:", StringComparison.OrdinalIgnoreCase) ? source[6..] : source;
            Icons.RegisterGlyph(fullName, char.ConvertFromUtf32(int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        }
    }

    public void RegisterTexts(string language, IReadOnlyDictionary<string, string> texts) => Languages.Register(Prefix, language, texts);

    public IDisposable Subscribe(string eventType, Action<NotificationEnvelope> handler)
    {
        var subscription = Router.Subscribe(eventType, handler);
        _subscriptions.Add(subscription);
        return subscription;
    }

    public void RegisterWorkflowStepType(IWorkflowStepHandler handler) => WorkflowRegistrar?.RegisterStepType(ModuleId, handler);

    public void RegisterWorkflowTemplate(WorkflowDefinition template) => WorkflowRegistrar?.RegisterTemplate(ModuleId, template);

    // ---- services

    public string GetText(string textKey) => Languages.GetText(textKey.Contains(':') ? textKey : $"{Prefix}:{textKey}");

    public void WriteLog(string message, bool isError = false) =>
        Log.Write(message, isError ? LogEntryType.Error : LogEntryType.Information, ModuleId);

    public void Notify(string title, string? message = null, NotificationSeverity severity = NotificationSeverity.Info) =>
        InApp.Show(title, message, severity);

    public void NavigateTo(string navigationKey, string? parameter = null) => Navigation.NavigateTo(navigationKey, parameter);

    public Task<bool> ExecuteCommandAsync(string commandId, object? parameter = null) => Commands.ExecuteAsync(commandId, parameter);

    /// <summary>Removes everything the module registered (called on unload and before a hot-swap).</summary>
    public void RemoveRegistrations()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        Backend.StateChanged -= OnConnectionChanged;
        Commands.UnregisterModule(ModuleId);
        Navigation.Registry.UnregisterModule(ModuleId);
        Icons.UnregisterPrefix(Prefix);
        Languages.Unregister(Prefix);
        WorkflowRegistrar?.UnregisterModule(ModuleId);
    }

    public override string ToString() => $"ModuleAppContext({ModuleId} {Version})";

    private void OnConnectionChanged(object? sender, EventArgs e) => ConnectionChanged?.Invoke(this, EventArgs.Empty);
}
