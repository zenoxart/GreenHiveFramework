using System.Windows.Threading;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>The <see cref="IDispatcherQueue"/> of a WPF window (its <see cref="Dispatcher"/>).</summary>
public sealed class WpfDispatcherQueue(Dispatcher dispatcher) : IDispatcherQueue
{
    public Dispatcher Dispatcher { get; } = dispatcher;

    public bool HasThreadAccess => Dispatcher.CheckAccess();

    public bool TryEnqueue(Action action, DispatchPriority priority = DispatchPriority.Normal)
    {
        if (Dispatcher.HasShutdownStarted)
        {
            return false;
        }

        Dispatcher.BeginInvoke(action, priority == DispatchPriority.Low ? DispatcherPriority.Background : DispatcherPriority.Normal);
        return true;
    }
}
