using System;
using System.Collections.Generic;
using System.Text;

namespace Zenox.Wpf.Dto.Example
{
    /// <summary>
    /// Repräsentiert einen Testdatensatz mit Name, Alter und Adresse.
    /// </summary>
    public record TestRecord()
    {
        /// <summary>
        /// Name der Person.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Alter der Person.
        /// </summary>
        public int Age { get; set; }

        /// <summary>
        /// Adresse der Person.
        /// </summary>
        public string Address { get; set; }
    }

    
}
