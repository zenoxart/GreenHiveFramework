using System.Collections.Concurrent;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.NET.NotificationContracts;

namespace GreenHive.Wpf.Framework.NotificationClient;

/// <summary>
/// Distributes received events to subscribers: exact type ("documents.invoice.approved"), prefix ("workflow.*") or all ("*").
/// Handlers run on the UI thread; a failing handler is logged and does not affect the others.
/// </summary>
public sealed class NotificationRouter : AppObject
{
    private readonly ConcurrentDictionary<Guid, (string Pattern, Action<NotificationEnvelope> Handler)> _subscriptions = new();

    public IDisposable Subscribe(string pattern, Action<NotificationEnvelope> handler)
    {
        var id = Guid.NewGuid();
        _subscriptions[id] = (pattern, handler);
        return new Subscription(() => _subscriptions.TryRemove(id, out _));
    }

    public void Publish(NotificationEnvelope envelope)
    {
        foreach (var (pattern, handler) in _subscriptions.Values)
        {
            if (!Matches(pattern, envelope.Type))
            {
                continue;
            }

            try
            {
                handler(envelope);
            }
            catch (Exception ex)
            {
                AppContext.Log.Write($"Handler for '{envelope.Type}' failed: {ex.Message}", LogEntryType.Error, nameof(NotificationRouter), ex);
            }
        }
    }

    public static bool Matches(string pattern, string type) =>
        pattern == "*" ||
        (pattern.EndsWith(".*", StringComparison.Ordinal) && type.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)) ||
        pattern.Equals(type, StringComparison.OrdinalIgnoreCase);

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
