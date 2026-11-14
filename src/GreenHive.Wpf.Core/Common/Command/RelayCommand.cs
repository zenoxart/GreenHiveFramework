using System.Diagnostics;
using System.Windows.Input;
using GreenHive.NET.Core.Common.CommandReplay;
using GreenHive.NET.Core.Common.Permission;

namespace GreenHive.NET.Core.Common.Command
{
    /// <summary>
    /// Stellt einen Befehl dar, der ausgeführt werden kann und dessen Ausführbarkeit anhand einer angegebenen Bedingung bestimmt wird.
    /// </summary>
    /// <remarks>Diese Klasse wird typischerweise verwendet, um eine Aktion und deren Ausführungslogik zu kapseln, 
    /// wodurch die Verwendung von Befehlen im MVVM (Model-View-ViewModel)-Muster ermöglicht wird. 
    /// Die Ausführbarkeit des Befehls wird durch ein Prädikat bestimmt, falls angegeben, oder ist standardmäßig immer möglich.</remarks>
    /// <remarks>
    /// Initialisiert einen neuen Befehl mit der angegebenen Ausführungs- und Prüf-Logik.
    /// </remarks>
    /// <param name="execute">Die auszuführende Aktion.</param>
    /// <param name="canExecute">Optional: Prädikat, das bestimmt, ob der Befehl ausgeführt werden kann.</param>
    /// <param name="requiredPermission">Optional: Die erforderliche Berechtigung, um den Befehl auszuführen.</param>
    public partial class RelayCommand(Action<object> execute, Predicate<object> canExecute = null!, PermissionType? requiredPermission = null) : ICommand
    {
        private readonly Action<object> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Predicate<object> _canExecute = canExecute;
        private readonly PermissionType? _requiredPermission = requiredPermission;

        /// <summary>
        /// Der Name des Befehls.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Die Kategorie des Befehls für das Command-Tracking.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gibt an, ob dieses Command vom CommandReplayManager getrackt werden soll.
        /// Standard: true
        /// </summary>
        public bool TrackExecution { get; set; } = true;

        /// <summary>
        /// Prüft, ob der Befehl ausgeführt werden kann.
        /// </summary>
        /// <param name="parameter">Optionaler Parameter.</param>
        /// <returns>True, wenn der Befehl ausgeführt werden kann, sonst false.</returns>
        public virtual bool CanExecute(object parameter)
        {
            return (_requiredPermission == null || PermissionManager.HasPermission(_requiredPermission.Value)) &&
                   (_canExecute == null || _canExecute(parameter));
        }

        /// <summary>
        /// Führt die hinterlegte Aktion aus und misst die Ausführungszeit.
        /// </summary>
        /// <param name="parameter">Optionaler Parameter.</param>
        public virtual void Execute(object parameter)
        {
            if (_requiredPermission != null && !PermissionManager.HasPermission(_requiredPermission.Value))
            {
                throw new UnauthorizedAccessException($"Die Berechtigung '{_requiredPermission}' ist erforderlich, um diesen Befehl auszuführen.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool success = true;
            string errorMessage = null;
            
            try
            {
                _execute(parameter);
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                RelayCommandPool.LogExecution(Name, stopwatch.Elapsed);
                
                // Automatisches Tracking über CommandReplayManager
                if (TrackExecution && !string.IsNullOrEmpty(Name))
                {
                    try
                    {
                        var recorded = CommandReplayManager.Instance.RecordCommand(
                            Name,
                            parameter,
                            Category ?? "Command");
                        
                        if (recorded != null)
                        {
                            recorded.DurationMs = stopwatch.ElapsedMilliseconds;
                            recorded.Success = success;
                            recorded.ErrorMessage = errorMessage;
                        }
                    }
                    catch
                    {
                        // Tracking-Fehler sollten die Command-Ausführung nicht beeinflussen
                    }
                }
            }
        }

        /// <summary>
        /// Wird ausgelöst, wenn sich die Ausführbarkeit des Befehls ändert.
        /// </summary>
        event EventHandler ICommand.CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
