namespace GreenHive.NET.CommandContracts;

/// <summary>Implementation of a command, provided by the host or a module.</summary>
public interface ICommandHandler
{
    /// <summary>Raised when <see cref="CanExecute"/> or the checked state may have changed.</summary>
    event EventHandler? CanExecuteChanged;

    bool CanExecute(object? parameter);

    Task ExecuteAsync(object? parameter, CancellationToken cancellationToken);

    /// <summary>Checked state for toggle commands; null for normal commands.</summary>
    bool? IsChecked(object? parameter) => null;

    /// <summary>Reason key (e.g. "noPermission") shown when the command is disabled.</summary>
    string? GetDisabledReason(object? parameter) => null;
}
