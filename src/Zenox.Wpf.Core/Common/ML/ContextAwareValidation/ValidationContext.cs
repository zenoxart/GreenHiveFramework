using System;
using System.Collections.Generic;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Repräsentiert den Kontext eines zu validierenden Wertes.
    /// Erfasst alle relevanten Informationen für die ML-basierte Validierung.
    /// </summary>
    public class ValidationContext
    {
        /// <summary>
        /// Eindeutige ID für diese Validierungsanfrage.
        /// </summary>
        public Guid RequestId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Zeitstempel der Validierungsanfrage.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.Now;

        /// <summary>
        /// Der zu validierende Wert (als object für Flexibilität).
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Der Wert als String-Repräsentation.
        /// </summary>
        public string? ValueAsString { get; set; }

        /// <summary>
        /// Name des Feldes/der Property (z.B. "Preis", "Menge", "Datum").
        /// </summary>
        public string? FieldName { get; set; }

        /// <summary>
        /// Typ des Wertes (z.B. "decimal", "int", "DateTime").
        /// </summary>
        public string? ValueType { get; set; }

        /// <summary>
        /// Name der View/des Screens wo der Wert angezeigt wird.
        /// </summary>
        public string? ViewName { get; set; }

        /// <summary>
        /// Name des ViewModels/DataContext.
        /// </summary>
        public string? ViewModelName { get; set; }

        /// <summary>
        /// Name der Entity/des Objekts zu dem der Wert gehört.
        /// </summary>
        public string? EntityName { get; set; }

        /// <summary>
        /// ID der Entity (falls vorhanden).
        /// </summary>
        public string? EntityId { get; set; }

        /// <summary>
        /// Umgebende/verwandte Werte aus demselben Kontext.
        /// Key = Feldname, Value = Wert
        /// </summary>
        public Dictionary<string, object?> RelatedValues { get; set; } = new();

        /// <summary>
        /// Historische Werte für dasselbe Feld (falls verfügbar).
        /// </summary>
        public List<object?> HistoricalValues { get; set; } = new();

        /// <summary>
        /// Benutzerdefinierte Metadaten für erweiterte Validierung.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Binding-Path des UI-Elements (falls über WPF Binding).
        /// </summary>
        public string? BindingPath { get; set; }

        /// <summary>
        /// Kategorie/Domäne des Wertes (z.B. "Finanzen", "Bestellung", "Lager").
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Erwarteter Wertebereich (Minimum).
        /// </summary>
        public double? ExpectedMin { get; set; }

        /// <summary>
        /// Erwarteter Wertebereich (Maximum).
        /// </summary>
        public double? ExpectedMax { get; set; }

        /// <summary>
        /// Gibt an, ob der Wert einen numerischen Typ hat.
        /// </summary>
        public bool IsNumeric => Value is IConvertible convertible &&
            (convertible is int or long or float or double or decimal or short or byte);

        /// <summary>
        /// Konvertiert den Wert zu double (falls numerisch).
        /// </summary>
        public double? AsDouble()
        {
            try
            {
                return Value switch
                {
                    null => null,
                    double d => d,
                    float f => f,
                    decimal dec => (double)dec,
                    int i => i,
                    long l => l,
                    short s => s,
                    byte b => b,
                    string str when double.TryParse(str, out var parsed) => parsed,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Erstellt eine String-Repräsentation des Kontexts für Logging/Debugging.
        /// </summary>
        public override string ToString()
        {
            return $"ValidationContext[Field={FieldName}, Value={ValueAsString}, View={ViewName}, Entity={EntityName}]";
        }
    }
}
