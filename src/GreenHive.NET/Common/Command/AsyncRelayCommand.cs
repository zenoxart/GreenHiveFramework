using System.Windows.Input;

namespace GreenHive.NET.Core.Common.Command
{
    /// <summary>
    /// Erweitert den RelayCommand, um asynchrone Ausführung zu unterstützen.
    /// Diese Klasse ermöglicht die Ausführung von zeitaufwändigen Operationen, ohne die Benutzeroberfläche zu blockieren.
    /// </summary>
    /// <remarks>
    /// AsyncRelayCommand implementiert das ICommand-Interface und bietet eine asynchrone Alternative zum standardmäßigen RelayCommand.
    /// Die Klasse verhindert automatisch die mehrfache gleichzeitige Ausführung und aktualisiert den CanExecute-Status entsprechend.
    /// Während der Ausführung wird das _isExecuting-Flag gesetzt, um Reentrancy-Probleme zu vermeiden.
    /// </remarks>
    public class AsyncRelayCommand : ICommand
    {
        /// <summary>
        /// Stellt einen Delegaten dar, der eine asynchrone Operation definiert, die ausgeführt werden soll.
        /// </summary>
        /// <remarks>
        /// Der Delegat nimmt einen Eingabeparameter vom Typ <see cref="object"/> entgegen und gibt einen <see cref="Task"/> zurück,
        /// der die asynchrone Operation repräsentiert. Dies kann verwendet werden, um benutzerdefinierte Logik asynchron zu kapseln und auszuführen.
        /// </remarks>
        private readonly Func<object, Task> _executeAsync;

        /// <summary>
        /// Delegat zur Bestimmung, ob der Befehl ausgeführt werden kann.
        /// </summary>
        private readonly Predicate<object> _canExecute;

        /// <summary>
        /// Flag, das angibt, ob der Befehl aktuell ausgeführt wird.
        /// </summary>
        private bool _isExecuting;

        /// <summary>
        /// Tritt auf, wenn sich die Bedingungen ändern, die beeinflussen, ob der Befehl ausgeführt werden soll.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Initialisiert eine neue Instanz der AsyncRelayCommand-Klasse.
        /// </summary>
        /// <param name="executeAsync">Die asynchrone Methode, die ausgeführt werden soll. Darf nicht null sein.</param>
        /// <param name="canExecute">Die Methode, die bestimmt, ob der Befehl ausgeführt werden kann. Kann null sein.</param>
        /// <exception cref="ArgumentNullException">Wird ausgelöst, wenn executeAsync null ist.</exception>
        /// <remarks>
        /// Wenn canExecute null ist, kann der Befehl immer ausgeführt werden (außer während einer laufenden Ausführung).
        /// </remarks>
        public AsyncRelayCommand(Func<object, Task> executeAsync, Predicate<object> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Bestimmt, ob der Befehl im aktuellen Zustand ausgeführt werden kann.
        /// </summary>
        /// <param name="parameter">Daten, die vom Befehl verwendet werden. Kann null sein, wenn der Befehl keine Daten benötigt.</param>
        /// <returns>
        /// true, wenn dieser Befehl ausgeführt werden kann; andernfalls false.
        /// Der Befehl kann nicht ausgeführt werden, wenn er bereits läuft oder wenn die canExecute-Bedingung false zurückgibt.
        /// </returns>
        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute(parameter));
        }

        /// <summary>
        /// Führt den asynchronen Befehl aus.
        /// </summary>
        /// <param name="parameter">Daten, die vom Befehl verwendet werden. Kann null sein, wenn der Befehl keine Daten benötigt.</param>
        /// <remarks>
        /// Diese Methode prüft zunächst, ob der Befehl ausgeführt werden kann. Falls nicht, wird die Methode frühzeitig beendet.
        /// Während der Ausführung wird das _isExecuting-Flag gesetzt und CanExecuteChanged ausgelöst, um die UI zu aktualisieren.
        /// Nach Abschluss der Operation (erfolgreich oder mit Ausnahme) wird das Flag zurückgesetzt.
        /// </remarks>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _executeAsync(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Löst das CanExecuteChanged-Ereignis aus, um Listener über Änderungen des Ausführungsstatus zu benachrichtigen.
        /// </summary>
        /// <remarks>
        /// Diese Methode wird automatisch aufgerufen, wenn sich der Ausführungsstatus ändert.
        /// Sie kann auch manuell aufgerufen werden, um eine Aktualisierung der UI-Elemente zu erzwingen,
        /// die an diesen Befehl gebunden sind.
        /// </remarks>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
