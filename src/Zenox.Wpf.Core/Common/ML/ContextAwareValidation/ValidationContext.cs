using System;
using System.Collections.Generic;

namespace Zenox.NET.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Kontext für die Validierung eines Wertes
    /// </summary>
    public class ValidationContext
    {
        public object? Value { get; set; }
        public string? ValueAsString { get; set; }
        public string? FieldName { get; set; }
        public string? ValueType { get; set; }
        public string? ViewName { get; set; }
        public string? ViewModelName { get; set; }
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public Dictionary<string, object?> RelatedValues { get; set; } = new();
        public List<object?> HistoricalValues { get; set; } = new();
        public string? BindingPath { get; set; }
        public string? Category { get; set; }
        public double? ExpectedMin { get; set; }
        public double? ExpectedMax { get; set; }

        public bool IsNumeric => Value is IConvertible convertible &&
            (convertible is int or long or float or double or decimal or short or byte);

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

        public override string ToString() =>
            $"ValidationContext[Field={FieldName}, Value={ValueAsString}, View={ViewName}]";
    }
}
