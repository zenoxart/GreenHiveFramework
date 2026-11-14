namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>
/// For ViewModels whose state must survive a hot-swap. The state may only contain simple types or
/// DTOs from the contracts - never types of the old module version.
/// </summary>
public interface IStatefulViewModel
{
    object? SaveState();

    void RestoreState(object? state);
}
