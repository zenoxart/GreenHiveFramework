namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// Base class of all application objects - services, managers, controllers (WIFI: AppObjekt).
/// Instances are produced by an <see cref="ApplicationContext"/>, never created with "new".
/// </summary>
public abstract class AppObject : IAppContext
{
    private ApplicationContext? _appContext;

    /// <summary>Raised when the object reports an error instead of throwing it into the UI.</summary>
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    /// <summary>The application infrastructure this object belongs to.</summary>
    public ApplicationContext AppContext
    {
        get => _appContext ?? throw new InvalidOperationException($"{GetType().Name} was not produced by an ApplicationContext.");
        set => _appContext = value;
    }

    /// <summary>True once the object was produced by a context.</summary>
    protected bool HasAppContext => _appContext is not null;

    public override string ToString() => GetType().Name;

    /// <summary>Called by the context right after it has been set (WIFI: production step).</summary>
    protected virtual void OnCreated()
    {
    }

    internal void NotifyCreated() => OnCreated();

    /// <summary>
    /// Raises <see cref="ErrorOccurred"/>. If nobody handles it, DEBUG builds rethrow the cause so
    /// errors always surface during development (WIFI behaviour of 2020-12-03).
    /// </summary>
    protected virtual void OnErrorOccurred(ErrorOccurredEventArgs e)
    {
        var handler = ErrorOccurred;
        if (handler is not null)
        {
            handler(this, e);
            return;
        }

#if DEBUG
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.Cause).Throw();
#endif
    }

    protected void OnErrorOccurred(Exception cause) => OnErrorOccurred(new ErrorOccurredEventArgs(cause));

    /// <summary>Returns the context as a specific service interface (e.g. ICommandContext).</summary>
    protected T Context<T>()
        where T : class =>
        AppContext as T ?? throw new InvalidOperationException($"The application context does not provide {typeof(T).Name}.");
}
