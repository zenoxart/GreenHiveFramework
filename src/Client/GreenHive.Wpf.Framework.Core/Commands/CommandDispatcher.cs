using System.Collections.Concurrent;
using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>Priority of a dispatched UI action.</summary>
public enum DispatchPriority
{
    Normal,
    Low,
}

/// <summary>The message queue of one UI thread (implemented for WPF by WpfDispatcherQueue).</summary>
public interface IDispatcherQueue
{
    bool HasThreadAccess { get; }

    bool TryEnqueue(Action action, DispatchPriority priority = DispatchPriority.Normal);
}

/// <summary>
/// The UI dispatcher of the application. Lives inside the RelayCommandPool (RelayCommandPool.Dispatcher)
/// and is exposed to the Mvvm layer as <see cref="IUiDispatcher"/>. Keeps one queue per window;
/// the main window's queue is the default.
/// </summary>
public sealed class CommandDispatcher : IUiDispatcher
{
    private readonly ConcurrentDictionary<string, IDispatcherQueue> _queues = new(StringComparer.OrdinalIgnoreCase);
    private IDispatcherQueue? _default;

    /// <summary>The default (main window) queue, or null before the first window registered.</summary>
    public IDispatcherQueue? Default => _default;

    public bool HasThreadAccess => _default?.HasThreadAccess ?? true;

    /// <summary>Registers the queue of a window (called by the WindowManager).</summary>
    public void Register(string windowKey, IDispatcherQueue queue, bool isDefault = false)
    {
        _queues[windowKey] = queue;
        if (isDefault || _default is null)
        {
            _default = queue;
        }
    }

    public void Unregister(string windowKey) => _queues.TryRemove(windowKey, out _);

    /// <summary>Runs on the UI thread; directly if already there.</summary>
    public void Run(Action action)
    {
        var queue = _default;
        if (queue is null || queue.HasThreadAccess)
        {
            action();
            return;
        }

        queue.TryEnqueue(action);
    }

    /// <summary>Always enqueues (used to coalesce several changes into one UI update).</summary>
    public void Post(Action action, DispatchPriority priority = DispatchPriority.Normal)
    {
        var queue = _default;
        if (queue is null)
        {
            action();
            return;
        }

        queue.TryEnqueue(action, priority);
    }

    public Task RunAsync(Action action)
    {
        var queue = _default;
        if (queue is null || queue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enqueued = queue.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        if (!enqueued)
        {
            completion.SetCanceled();
        }

        return completion.Task;
    }
}
