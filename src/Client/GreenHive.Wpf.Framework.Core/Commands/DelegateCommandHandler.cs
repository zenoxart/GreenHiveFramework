using GreenHive.NET.CommandContracts;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>Convenient <see cref="ICommandHandler"/> built from delegates.</summary>
public sealed class DelegateCommandHandler : ICommandHandler
{
    private readonly Func<object?, CancellationToken, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Func<object?, bool?>? _isChecked;

    public DelegateCommandHandler(Func<object?, CancellationToken, Task> execute, Func<object?, bool>? canExecute = null, Func<object?, bool?>? isChecked = null)
    {
        _execute = execute;
        _canExecute = canExecute;
        _isChecked = isChecked;
    }

    public DelegateCommandHandler(Action<object?> execute, Func<object?, bool>? canExecute = null, Func<object?, bool?>? isChecked = null)
        : this((parameter, _) =>
        {
            execute(parameter);
            return Task.CompletedTask;
        }, canExecute, isChecked)
    {
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public Task ExecuteAsync(object? parameter, CancellationToken cancellationToken) => _execute(parameter, cancellationToken);

    public bool? IsChecked(object? parameter) => _isChecked?.Invoke(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
