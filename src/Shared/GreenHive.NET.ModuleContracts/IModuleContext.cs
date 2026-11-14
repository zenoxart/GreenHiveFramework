using GreenHive.NET.CommandContracts;
using GreenHive.NET.Monads;
using GreenHive.NET.NotificationContracts;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.ModuleContracts;

/// <summary>A view a module offers: navigation entry, dock role and factories (UI-free signature).</summary>
public sealed record ModuleViewRegistration(string Key)
{
    public string? TextKey { get; init; }

    public string? Icon { get; init; }

    public ViewDescriptor View { get; init; } = new();

    /// <summary>The module's home view becomes its segment in the navigation wheel.</summary>
    public bool IsModuleHome { get; init; }

    public string? Group { get; init; }

    public int Order { get; init; }

    public bool ShowInViewMenu { get; init; }

    public string? Permission { get; init; }

    /// <summary>Creates the ViewModel (a ViewModelAppObject produced by the module context).</summary>
    public required Func<object> CreateViewModel { get; init; }

    /// <summary>Creates the view (a FrameworkElement) for a ViewModel.</summary>
    public required Func<object, object> CreateView { get; init; }
}

/// <summary>The signed-in user as seen by modules.</summary>
public sealed record ModuleUser(Guid Id, string UserName, string DisplayName, IReadOnlyCollection<string> Permissions)
{
    public static ModuleUser Anonymous { get; } = new(Guid.Empty, "anonymous", "Anonymous", []);

    public bool Has(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) || Permissions.Contains("*");
}

/// <summary>
/// What the host makes available to a module (UI-free). Implemented by the ModuleAppContext of the ModuleLoader, which
/// is also the module's ApplicationContext. Every registration is tracked and removed automatically when the module unloads.
/// </summary>
public interface IModuleContext
{
    string ModuleId { get; }

    string Version { get; }

    ModuleManifest Manifest { get; }

    /// <summary>The module's own data folder (AppData).</summary>
    string DataPath { get; }

    /// <summary>Client for the module's backend route (base address = gateway + backend.route, bearer token attached).</summary>
    IModuleApi Api { get; }

    ModuleUser CurrentUser { get; }

    bool IsConnected { get; }

    event EventHandler? ConnectionChanged;

    // ---- registrations (tracked, removed on unload) ----

    void RegisterCommand(CommandDescriptor descriptor, ICommandHandler handler);

    void RegisterGroup(CommandGroupDescriptor group);

    void RegisterView(ModuleViewRegistration view);

    /// <summary>Registers a module icon, available as "&lt;moduleId&gt;.&lt;name&gt;" (glyph code like "E8A5" or "image:...").</summary>
    void RegisterIcon(string name, string source);

    /// <summary>Registers the module's texts for one language under the module's TextKey scope.</summary>
    void RegisterTexts(string language, IReadOnlyDictionary<string, string> texts);

    IDisposable Subscribe(string eventType, Action<NotificationEnvelope> handler);

    void RegisterWorkflowStepType(IWorkflowStepHandler handler);

    void RegisterWorkflowTemplate(WorkflowDefinition template);

    // ---- services ----

    string GetText(string textKey);

    void WriteLog(string message, bool isError = false);

    void Notify(string title, string? message = null, NotificationSeverity severity = NotificationSeverity.Info);

    void NavigateTo(string navigationKey, string? parameter = null);

    Task<bool> ExecuteCommandAsync(string commandId, object? parameter = null);
}

/// <summary>HTTP access to the module backend; every call returns a Result (no exceptions for expected failures).</summary>
public interface IModuleApi
{
    Task<Result<T>> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken = default);

    Task<Result<T>> PostAsync<T>(string relativeUrl, object? body, CancellationToken cancellationToken = default);

    Task<Result<T>> PutAsync<T>(string relativeUrl, object? body, CancellationToken cancellationToken = default);

    Task<Result<Unit>> DeleteAsync(string relativeUrl, CancellationToken cancellationToken = default);
}
