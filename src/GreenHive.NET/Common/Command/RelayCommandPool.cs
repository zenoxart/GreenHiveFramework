using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using GreenHive.Wpf.Dto;

namespace GreenHive.NET.Core.Common.Command
{
    /// <summary>
    /// Verwaltet eine Sammlung von RelayCommands, die über eindeutige Schlüssel referenziert werden können.
    /// Ermöglicht das einfache Binden von Commands anhand ihres Schlüssels, z.B. in MVVM-Szenarien.
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
    public class RelayCommandPool : Dictionary<string, RelayObject>
    {

        #region Log-Execution

        private static ObservableCollection<CommandRecord> _executionLog = new();

        public ObservableCollection<CommandRecord> ExecutionLog
        {
            get { return _executionLog; }
            set { _executionLog = value; }
        }

        public static void LogExecution(string commandName, TimeSpan executionTime)
        {
            CommandRecord logEntryRecord = new()
            {
                Name = string.IsNullOrEmpty(commandName) ? "[Unbenannter Befehl]" : commandName,
                Ausführungszeit = executionTime.TotalMilliseconds,
            };

            // Fallback, falls kein Dispatcher verfügbar ist
            _executionLog.Add(logEntryRecord);
        }

        #endregion



        /// <summary>
        /// Fügt ein RelayObject zum Pool hinzu und stellt sicher, dass der Befehlsname synchronisiert wird.
        /// </summary>
        /// <param name="key">Der Schlüssel für das RelayObject</param>
        /// <param name="value">Das hinzuzufügende RelayObject</param>
        public new void Add(string key, RelayObject value)
        {
            // Sicherstellen, dass der Name des RelayObjects mit dem Schlüssel übereinstimmt, falls er nicht bereits gesetzt ist
            if (string.IsNullOrEmpty(value.Name))
            {
                value.Name = key;
            }

            // Sicherstellen, dass der Befehlsname synchronisiert ist
            if (value.Command != null && string.IsNullOrEmpty(value.Command.Name))
            {
                value.Command.Name = value.Name;
            }

            base.Add(key, value);
        }

        /// <summary>
        /// Indexer, der beim Setzen von Werten die Synchronisation des Befehlsnamens sicherstellt.
        /// </summary>
        /// <param name="key">Der Schlüssel</param>
        /// <returns>Das RelayObject</returns>
        public new RelayObject this[string key]
        {
            get => base[key];
            set
            {
                // Sicherstellen, dass der Name des RelayObjects mit dem Schlüssel übereinstimmt, falls er nicht bereits gesetzt ist
                if (string.IsNullOrEmpty(value.Name))
                {
                    value.Name = key;
                }

                // Sicherstellen, dass der Befehlsname synchronisiert ist
                if (value.Command != null && string.IsNullOrEmpty(value.Command.Name))
                {
                    value.Command.Name = value.Name;
                }

                base[key] = value;
            }
        }

        /// <summary>
        /// Bestimmt, ob zwei angegebene Objekte gleich sind.
        /// </summary>
        /// <param name="obj1">Das erste zu vergleichende Objekt oder <see langword="null"/>.</param>
        /// <param name="obj2">Das zweite zu vergleichende Objekt oder <see langword="null"/>.</param>
        /// <returns><see langword="true"/> wenn die Objekte als gleich betrachtet werden; andernfalls <see langword="false"/>.</returns>
        public static bool ActivatorEqualCheck(object? obj1, object? obj2)
        {
            return Activator.Equals(obj1, obj2);
        }

        /// 
        /// Versucht, einen  abzurufen, der dem angegebenen Schlüssel zugeordnet ist.
        /// 
        /// Diese Methode löst keine Ausnahme aus, wenn der Schlüssel nicht gefunden wird. Stattdessen gibt sie
        ///  zurück und setzt  auf seinen Standardwert.
        /// Der Schlüssel, der dem gewünschten  zugeordnet ist.
        /// Wenn diese Methode zurückkehrt, enthält sie den , der dem angegebenen Schlüssel zugeordnet ist, wenn
        /// der Schlüssel gefunden wird; andernfalls den Standardwert für den Typ des Befehlsparameters.
        ///  wenn ein  mit dem angegebenen Schlüssel gefunden wird; andernfalls <see
        /// langword="false"/>.
        public bool TryGetCommand(string key, out RelayCommand command)
        {
            RelayObject obj;

            bool returnvalue = this.TryGetValue(key, out obj);

            command = obj.Command;

            return returnvalue;
        }

        /// <summary>
        /// Ruft den Befehl ab, der dem angegebenen Schlüssel zugeordnet ist.
        /// </summary>
        /// <param name="key">Der Schlüssel, der dem abzurufenden Befehl zugeordnet ist. Darf nicht null oder leer sein.</param>
        /// <returns>Der <see cref="ICommand"/> Befehl, der dem angegebenen Schlüssel zugeordnet ist.</returns>
        /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn der angegebene <paramref name="key"/> nicht existiert oder keinen gültigen Befehl enthält.</exception>
        public ICommand GetCommand(string key)
        {
            if (this.TryGetValue(key, out RelayObject command) && command.Command != null)
            {
                return command.Command;
            }
            throw new KeyNotFoundException($"Schlüssel '{key}' wurde nicht gefunden oder enthält keinen gültigen Befehl.");
        }

        /// <summary>
        /// Ruft das ICommand für den angegebenen Schlüssel ab, um Rückwärtskompatibilität mit XAML-Bindungen zu gewährleisten.
        /// Verwendung für Befehlsbindung: CommandPool.GetICommand("schlüssel")
        /// </summary>
        /// <param name="key">Der Befehlsschlüssel</param>
        /// <returns>Die ICommand-Instanz</returns>
        public ICommand GetICommand(string key)
        {
            if (this.TryGetValue(key, out RelayObject relayObject) && relayObject.Command != null)
            {
                return relayObject.Command;
            }
            throw new KeyNotFoundException($"Schlüssel '{key}' wurde nicht gefunden oder enthält keinen gültigen Befehl.");
        }

        /// <summary>
        /// Bietet Zugriff auf ICommand-Instanzen über Indexer-Syntax für XAML-Bindungskompatibilität.
        /// Beispiel: CommandPool.Commands["schlüssel"] in XAML-Bindungen
        /// </summary>
        public CommandAccessor Commands => new(this);

        /// <summary>
        /// Hilfsklasse, um ICommand-Zugriff über Indexer für XAML-Bindung bereitzustellen
        /// </summary>
        public class CommandAccessor
        {
            /// <summary>
            /// Internes Pool-Objekt, das die RelayCommands enthält.
            /// </summary>
            private readonly RelayCommandPool _pool;

            /// <summary>
            /// Initialisiert eine neue Instanz der <see cref="CommandAccessor"/> Klasse mit dem angegebenen Befehlspool.
            /// </summary>
            /// <remarks>Dieser Konstruktor ist nur für die interne Verwendung vorgesehen und initialisiert den
            /// Befehlszugriff mit einem gegebenen Befehlspool.</remarks>
            /// <param name="pool">Die <see cref="RelayCommandPool"/> Instanz, die zum Verwalten von Befehlsinstanzen verwendet wird.</param>
            internal CommandAccessor(RelayCommandPool pool)
            {
                _pool = pool;
            }

            /// <summary>
            /// Ruft den <see cref="ICommand"/> ab, der dem angegebenen Schlüssel zugeordnet ist.
            /// </summary>
            /// <param name="key">Der Schlüssel des abzurufenden Befehls. Darf nicht null oder leer sein.</param>
            /// <returns>Der <see cref="ICommand"/> Befehl, der dem angegebenen Schlüssel zugeordnet ist.</returns>
            /// <exception cref="KeyNotFoundException">Wird ausgelöst, wenn der angegebene Schlüssel nicht im Pool existiert oder der zugeordnete Befehl null ist.</exception>
            public ICommand this[string key]
            {
                get
                {
                    if (_pool.TryGetValue(key, out RelayObject relayObject) && relayObject.Command != null)
                    {
                        return relayObject.Command;
                    }
                    throw new KeyNotFoundException($"Schlüssel '{key}' wurde nicht gefunden oder enthält keinen gültigen Befehl.");
                }
            }
        }


    }

    /// <summary>
    /// Erweiterungsmethoden für RelayCommandPool.
    /// </summary>
    public static class RelayCommandPoolExtensions
    {
        /// <summary>
        /// Führt den Command mit dem angegebenen Schlüssel aus, falls vorhanden.
        /// </summary>
        /// <param name="pool">Der RelayCommandPool.</param>
        /// <param name="key">Der Schlüssel.</param>
        /// <param name="parameter">Optionales Parameterobjekt.</param>
        /// <returns>True, wenn ausgeführt, sonst false.</returns>
        public static bool ExecuteIfExists(this RelayCommandPool pool, string key, object parameter = null)
        {
            RelayCommand command;
            if (pool.TryGetCommand(key, out command) && command.CanExecute(parameter))
            {
                command.Execute(parameter);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gibt alle Schlüssel als Liste zurück.
        /// </summary>
        public static List<string> GetAllKeys(this RelayCommandPool pool)
        {
            return pool.Keys.ToList();
        }

        /// <summary>
        /// Ermöglicht Binding auf den Namen eines RelayCommands im Pool.
        /// Beispiel: Binding AppContext.RelayCentral.CommandPool.Commands[Open].Name
        /// </summary>
        /// <param name="pool">Der RelayCommandPool.</param>
        /// <param name="key">Der Schlüssel.</param>
        /// <returns>Name des RelayCommands oder null.</returns>
        public static string GetCommandName(this RelayCommandPool pool, string key)
        {
            if (pool.TryGetCommand(key, out var command))
                return command?.Name;
            return null;
        }
    }
}
