using System.Collections.Concurrent;
using GreenHive.Wpf.Framework.Commands;

namespace GreenHive.Wpf.Framework.HotSwap;

/// <summary>
/// Counts running command executions per module. During a swap new executions of that module are rejected and,
/// after the grace period, running ones are cancelled.
/// </summary>
public sealed class ModuleQuiescence : ICommandExecutionGate
{
    private readonly ConcurrentDictionary<string, ModuleState> _modules = new(StringComparer.OrdinalIgnoreCase);

    public int Running(string moduleId) => _modules.TryGetValue(moduleId, out var s) ? s.Running : 0;

    public (IDisposable Scope, CancellationToken Token)? Enter(string moduleId)
    {
        var state = _modules.GetOrAdd(moduleId, _ => new ModuleState());
        lock (state)
        {
            if (state.Blocked)
            {
                return null;
            }

            state.Running++;
            return (new Scope(state), state.Cancellation.Token);
        }
    }

    /// <summary>Blocks the module and waits until its commands finished; cancels them after <paramref name="grace"/>.</summary>
    public async Task<IDisposable> QuiesceAsync(string moduleId, TimeSpan grace, CancellationToken ct = default)
    {
        var state = _modules.GetOrAdd(moduleId, _ => new ModuleState());
        lock (state)
        {
            state.Blocked = true;
        }

        var deadline = DateTimeOffset.UtcNow + grace;
        while (state.Running > 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, ct);
        }

        if (state.Running > 0)
        {
            await state.Cancellation.CancelAsync();
            var hard = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
            while (state.Running > 0 && DateTimeOffset.UtcNow < hard)
            {
                await Task.Delay(50, ct);
            }
        }

        return new Release(this, moduleId);
    }

    private void Unblock(string moduleId)
    {
        if (_modules.TryGetValue(moduleId, out var state))
        {
            lock (state)
            {
                state.Blocked = false;
                if (state.Cancellation.IsCancellationRequested)
                {
                    state.Cancellation.Dispose();
                    state.Cancellation = new CancellationTokenSource();
                }
            }
        }
    }

    private sealed class ModuleState
    {
        public int Running;

        public bool Blocked;

        public CancellationTokenSource Cancellation = new();
    }

    private sealed class Scope(ModuleState state) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                lock (state)
                {
                    state.Running--;
                }
            }
        }
    }

    private sealed class Release(ModuleQuiescence owner, string moduleId) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unblock(moduleId);
            }
        }
    }
}
