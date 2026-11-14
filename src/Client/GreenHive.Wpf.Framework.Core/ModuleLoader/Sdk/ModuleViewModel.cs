using GreenHive.NET.ModuleContracts;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>
/// ViewModel base for Result-based work: <see cref="RunAsync{T}"/> runs a call with busy state and turns a failure into a
/// localized <see cref="ErrorText"/>; texts and permissions are one call away.
/// </summary>
public abstract class ResultViewModel : ViewModelAppObject
{
    private string? _errorText;
    private string? _infoText;
    private readonly CancellationTokenSource _closed = new();

    public string? ErrorText
    {
        get => _errorText;
        protected set => SetProperty(ref _errorText, value);
    }

    public string? InfoText
    {
        get => _infoText;
        protected set => SetProperty(ref _infoText, value);
    }

    protected CancellationToken Closed => _closed.Token;

    protected BackendConnection Backend => Context<ISecurityContext>().Backend;

    public virtual string T(string key) => AppContext.Languages.GetText(key);

    public virtual string T(string key, params (string Name, object? Value)[] parameters) =>
        AppContext.Languages.GetText(key, parameters.ToDictionary(p => p.Name, p => p.Value));

    public bool Has(string permission) => Backend.Has(permission);

    public string Format(Error error) => ErrorMessages.Format(AppContext.Languages, error);

    public string Text(string keyOrText) => ErrorMessages.Text(AppContext.Languages, keyOrText);

    public void ClearMessages()
    {
        ErrorText = null;
        InfoText = null;
    }

    /// <summary>Runs a call; on success <paramref name="onSuccess"/> runs, on failure <see cref="ErrorText"/> shows the localized error.</summary>
    protected async Task<bool> RunAsync<T>(Func<CancellationToken, Task<Result<T>>> call, Action<T>? onSuccess = null, string? successInfo = null)
    {
        BeginWork();
        ErrorText = null;
        try
        {
            var result = await call(Closed);
            if (result.TryGetValue(out var value, out var error))
            {
                onSuccess?.Invoke(value);
                InfoText = successInfo;
                return true;
            }

            ErrorText = Format(error);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            EndWork();
        }
    }

    public override void OnClosed()
    {
        _closed.Cancel();
        base.OnClosed();
    }
}

/// <summary>ViewModel base of modules: the module context, its API client and texts with the module's TextKey scope.</summary>
public abstract class ModuleViewModel : ResultViewModel
{
    protected ModuleAppContext Module => Context<ModuleAppContext>();

    protected IModuleApi Api => Module.Api;

    public override string T(string key) => Module.GetText(key);

    public override string T(string key, params (string Name, object? Value)[] parameters) =>
        AppContext.Languages.GetText(key.Contains(':') ? key : $"{Module.Prefix}:{key}", parameters.ToDictionary(p => p.Name, p => p.Value));
}
