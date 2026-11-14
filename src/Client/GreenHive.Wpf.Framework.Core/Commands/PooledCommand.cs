using System.Windows.Input;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>
/// The stable ICommand that WPF controls bind to. Holds only its ID and the pool - never a reference
/// to a module type - so an old module version can be unloaded after a hot-swap.
/// </summary>
public sealed class PooledCommand : ICommand
{
    private readonly RelayCommandPool _pool;

    internal PooledCommand(RelayCommandPool pool, string id)
    {
        _pool = pool;
        Id = id;
    }

    public event EventHandler? CanExecuteChanged;

    public string Id { get; }

    public bool CanExecute(object? parameter) => _pool.CanExecute(Id, parameter);

    public async void Execute(object? parameter) => await _pool.ExecuteAsync(Id, parameter);

    internal void RaiseCanExecuteChanged() => _pool.Dispatcher.Run(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
}
