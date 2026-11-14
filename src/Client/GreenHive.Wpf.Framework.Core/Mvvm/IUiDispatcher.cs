namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// Minimal UI dispatcher contract. The implementation lives inside the central command manager
/// (RelayCommandPool.Dispatcher); the client context points <see cref="ApplicationContext.Dispatcher"/> at it.
/// </summary>
public interface IUiDispatcher
{
    bool HasThreadAccess { get; }

    /// <summary>Runs the action on the UI thread (directly if already there).</summary>
    void Run(Action action);

    /// <summary>Runs the action on the UI thread and completes when it has run.</summary>
    Task RunAsync(Action action);
}
