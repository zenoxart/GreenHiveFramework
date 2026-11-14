using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using GreenHive.NET.Core.Common.Command;

namespace GreenHive.Wpf.UI.MVVM.ViewModel.Manager
{
    /// <summary>
    /// Dient als Basisklasse zur Verwaltung von Relay-Objekten und Menüs innerhalb der Anwendung.
    /// </summary>
    /// <remarks>
    /// Die <see cref="BaseManager"/>-Klasse ist dafür verantwortlich, alle öffentlichen
    /// Eigenschaften vom Typ <see cref="RelayObject"/> und <see cref="RelayMenu"/> bei der Instanziierung automatisch
    /// in einen Befehlspool zu registrieren. Dies erleichtert die Verwaltung und Ausführung von
    /// Befehlen, die mit diesen Objekten verbunden sind. Die automatische Registrierung erfolgt über
    /// Reflection und gewährleistet, dass alle benannten RelayObjects für die Befehlsausführung verfügbar sind.
    /// </remarks>
    public abstract class BaseManager : Common.UINotificationObject
    {
        /// <summary>
        /// Initialisiert eine neue Instanz der <see cref="BaseManager"/>-Klasse.
        /// </summary>
        /// <remarks>
        /// Dieser Konstruktor registriert automatisch alle Relay-Objekte und Menüs bei der Initialisierung.
        /// Die Registrierung erfolgt durch Aufruf der <see cref="RegisterAllRelayObjectsAndMenus"/>-Methode.
        /// </remarks>
        public BaseManager()
        {
            RegisterAllRelayObjectsAndMenus();
        }

        /// <summary>
        /// Registriert alle <see cref="RelayObject"/>- und <see cref="RelayMenu"/>-Instanzen aus den öffentlichen Eigenschaften
        /// der aktuellen Instanz im Befehlspool.
        /// </summary>
        /// <remarks>
        /// Diese Methode durchläuft alle öffentlichen Instanz-Eigenschaften des aktuellen Objekts, die
        /// vom Typ <see cref="RelayObject"/> oder <see cref="RelayMenu"/> sind. Sie fügt jedes <see cref="RelayObject"/>
        /// mit einem nicht-leeren Namen zum Befehlspool hinzu. Für <see cref="RelayMenu"/>-Eigenschaften wird jedes enthaltene
        /// <see cref="RelayObject"/> mit einem nicht-leeren Namen zum Befehlspool hinzugefügt.
        /// Die Registrierung erfolgt über Reflection und ermöglicht eine automatische Erkennung aller befehlsrelevanten Objekte.
        /// </remarks>
        private void RegisterAllRelayObjectsAndMenus()
        {
            // Lade alle öffentlichen Eigenschaften die RelayObject sind
            IEnumerable<PropertyInfo> relayObjectProps = this.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(RelayObject).IsAssignableFrom(p.PropertyType));

            // Für jede RelayObject-Eigenschaft
            foreach (PropertyInfo prop in relayObjectProps)
            {
                var relayObj = prop.GetValue(this) as RelayObject;
                if (relayObj != null && !string.IsNullOrEmpty(relayObj.Name))
                {
                    // Füge das RelayObject zum CommandPool hinzu, wenn es einen Namen hat
                    CommandPool.Add(relayObj.Name, relayObj);
                }
            }

            // Lade alle öffentlichen Eigenschaften die von RelayMenu sind
            var relayMenuProps = this.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(RelayMenu).IsAssignableFrom(p.PropertyType));

            // Für jede RelayMenu-Eigenschaft
            foreach (var prop in relayMenuProps)
            {
                var relayMenu = prop.GetValue(this) as RelayMenu;
                if (relayMenu != null)
                {
                    foreach (var relayObj in relayMenu)
                    {
                        if (relayObj != null && !string.IsNullOrEmpty(relayObj.Name))
                        {
                            // Füge jedes RelayObject im RelayMenu zum CommandPool hinzu, wenn es einen Namen hat
                            CommandPool.Add(relayObj.Name, relayObj);
                        }
                    }
                }
            }
#if DEBUG
            var debugPool = this.CommandPool;
#endif
        }
    }
}
