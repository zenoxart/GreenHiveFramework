using GreenHive.Wpf.Framework.Mvvm;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>Result data of a command execution.</summary>
public sealed record CommandExecutedEventArgs(string CommandId, object? Parameter, Exception? Error);

/// <summary>Lets the hot-swap layer count running executions per module and block a module while it is swapped.</summary>
public interface ICommandExecutionGate
{
    /// <summary>Returns null when the module is being swapped; otherwise a scope ended after the execution and a token cancelled on swap.</summary>
    (IDisposable Scope, CancellationToken Token)? Enter(string moduleId);
}

/// <summary>
/// Runs commands uniformly: permission check (UI gating only - the server authorizes again),
/// CanExecute, asynchronous execution, error reporting to the log instead of crashing the UI.
/// </summary>
public sealed class CommandExecutor : AppObject
{
    /// <summary>Raised after every execution (also failed ones).</summary>
    public event EventHandler<CommandExecutedEventArgs>? Executed;

    /// <summary>Permission check; returns true if the current user has the permission. Default: allow.</summary>
    public Func<string, bool>? PermissionCheck { get; set; }

    public bool HasPermission(string? permission) => permission is null || PermissionCheck?.Invoke(permission) != false;

    /// <summary>Optional gate (ModuleQuiescence) - module commands are counted and can be cancelled during a swap.</summary>
    public ICommandExecutionGate? Gate { get; set; }

    public async Task<bool> ExecuteAsync(CommandRegistration registration, object? parameter, CancellationToken cancellationToken = default)
    {
        var id = registration.Descriptor.Id;
        if (!HasPermission(registration.Descriptor.Permission))
        {
            AppContext.Log.Write($"Command '{id}' denied: missing permission '{registration.Descriptor.Permission}'", LogEntryType.Warning, nameof(CommandExecutor));
            return false;
        }

        var gate = Gate?.Enter(registration.ModuleId);
        if (Gate is not null && gate is null)
        {
            AppContext.Log.Write($"Command '{id}' rejected: module '{registration.ModuleId}' is being updated", LogEntryType.Information, nameof(CommandExecutor));
            return false;
        }

        using var linked = gate is { } g ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, g.Token) : null;
        try
        {
            if (!registration.Handler.CanExecute(parameter))
            {
                return false;
            }

            await registration.Handler.ExecuteAsync(parameter, linked?.Token ?? cancellationToken);
            Executed?.Invoke(this, new CommandExecutedEventArgs(id, parameter, null));
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            OnErrorOccurred(new InvalidOperationException($"Command '{id}' failed: {ex.Message}", ex));
            Executed?.Invoke(this, new CommandExecutedEventArgs(id, parameter, ex));
            return false;
        }
        finally
        {
            gate?.Scope.Dispose();
        }
    }
}
