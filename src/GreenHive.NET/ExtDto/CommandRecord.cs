using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace GreenHive.Wpf.Dto
{
    /// <summary>
    /// Präsentiert einen Befehl mit einer ID, einem Namen und einer Aktion und der Ausführungszeit.
    /// </summary>
    public record CommandRecord()
    {
        /// <summary>
        /// Name des Befehls.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Ausführungszeit des Befehls.
        /// </summary>
        public double Ausführungszeit { get; set; }
    }

}
