using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Security;
using GreenHive.Wpf.UI.Platform.Shell;

namespace GreenHive.Wpf.UI.Platform;

/// <summary>
/// Runs once the main window is visible:
///   1. restore the remembered session, otherwise show the sign-in dialog (or continue offline),
///   2. load the modules (catalogue of the signed-in user + local/development packages),
///   3. restore the dock layout (now that module views exist),
///   4. connect the notification stream, refresh the workflow inbox, start the hot-swap manager, register toasts.
/// Sign-out and sign-in later on repeat steps 2 and 4.
/// </summary>
internal sealed class StartupSequence(ClientAppContext context, ShellWindow window)
{
    private bool _layoutRestored;

    /// <summary>Status line for the shell while starting.</summary>
    public event EventHandler<string>? Status;

    public async Task RunAsync()
    {
        var log = context.Log;
        Report("startup.signingin");

        var restored = await context.Backend.RestoreSessionAsync();
        if (restored.IsFailure)
        {
            log.Write($"No remembered session ({restored.Error.Code})", LogEntryType.Information, nameof(StartupSequence));
            await LoginDialog.ShowAsync(window, context);
        }

        await LoadModulesAsync();

        context.Backend.SignedIn += async (_, _) => await OnSignedInAsync();
        context.Backend.SignedOut += async (_, _) => await OnSignedOutAsync();
        context.Backend.PermissionsChanged += (_, _) => context.Commands.RequeryAll();
        context.Router.Subscribe(EventTypes.PermissionsChanged, e => _ = RefreshPermissionsAsync());

        StartConnectedServices();
        log.Write("Startup finished", LogEntryType.Information, nameof(StartupSequence));
    }

    /// <summary>Shows the sign-in dialog (menu command "Sign in").</summary>
    public Task<bool> SignInAsync() => LoginDialog.ShowAsync(window, context);

    private async Task LoadModulesAsync()
    {
        Report("startup.modules");
        await context.Modules.LoadAllAsync();
        if (!_layoutRestored)
        {
            _layoutRestored = true;
            context.Dock.RestoreLayout(BuiltInViews.DefaultLayout);
        }

        var loaded = context.Modules.Loaded.Count;
        var problems = context.Modules.Problems.Count;
        if (problems > 0)
        {
            var languages = context.Languages;
            var title = languages.GetText("shell:startup.problems", new Dictionary<string, object?> { ["count"] = problems });
            var details = string.Join("\n", context.Modules.Problems.Select(p => $"{p.ModuleId}: {ErrorMessages.Text(languages, p.Reason)}"));
            context.InApp.Show(title, details, NotificationSeverity.Warning, BuiltInViews.Output);
        }

        Report(context.Backend.IsSignedIn ? "startup.ready" : "startup.offline", ("count", loaded));
    }

    private void StartConnectedServices()
    {
        _ = context.InApp;
        context.Toasts.Navigate = (target, parameter) => context.Navigation.NavigateTo(target, parameter);
        context.Toasts.IsForeground = () => window.IsActive;
        context.Toasts.Register();
        context.InApp.Added += (_, item) => context.Toasts.Show(item);
        context.HotSwap.Start();
        if (context.Backend.IsSignedIn)
        {
            context.Hub.Start();
            _ = context.Workflows.RefreshInboxAsync();
        }
    }

    private async Task OnSignedInAsync()
    {
        context.Hub.Start();
        await LoadModulesAsync();
        context.Commands.RequeryAll();
    }

    private async Task OnSignedOutAsync()
    {
        context.Hub.Stop();
        foreach (var module in context.Modules.Loaded.ToList())
        {
            await context.Modules.UnloadAsync(module.Manifest.ModuleId);
        }

        context.Commands.RequeryAll();
        Report("connection.signedout");
    }

    private async Task RefreshPermissionsAsync()
    {
        var refreshed = await context.Backend.RefreshAsync();
        if (refreshed.IsSuccess)
        {
            await LoadModulesAsync();
        }
    }

    private void Report(string key, params (string Name, object? Value)[] values) =>
        Status?.Invoke(this, context.Languages.GetText("shell:" + key, values.ToDictionary(v => v.Name, v => v.Value)));
}
