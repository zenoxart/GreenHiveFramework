using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Zenox.Wpf.Core.Common.MVVM.FactoryInjection;
using Zenox.Wpf.Core.Common.Services;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Service für kontextbewusste ML-basierte Validierung von Werten.
    /// Kombiniert ML.NET mit StateMachine für intelligente Wertprüfung und visuelles Feedback.
    /// </summary>
    public class ContextAwareValidationService : AppObject
    {
        private static ContextAwareValidationService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// Singleton-Instanz des Services.
        /// </summary>
        public static ContextAwareValidationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ContextAwareValidationService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// MLContext für ML.NET-Operationen.
        /// </summary>
        public MLContext MLContext { get; } = new MLContext(seed: 42);

        /// <summary>
        /// Cache für historische Werte pro Feld/Kategorie.
        /// Key = "{Category}:{FieldName}"
        /// </summary>
        private readonly ConcurrentDictionary<string, List<HistoricalValueEntry>> _valueHistory = new();

        /// <summary>
        /// Cache für berechnete Statistiken pro Feld.
        /// </summary>
        private readonly ConcurrentDictionary<string, FieldStatistics> _fieldStatistics = new();

        /// <summary>
        /// Aktive StateMachines pro Element (z.B. nach Element-ID).
        /// </summary>
        private readonly ConcurrentDictionary<string, ValidationStateMachine> _activeStateMachines = new();

        /// <summary>
        /// Konfiguration für Anomalie-Erkennung.
        /// </summary>
        public AnomalyDetectionConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// Event das gefeuert wird, wenn eine Validierung abgeschlossen ist.
        /// </summary>
        public event EventHandler<ValidationCompletedEventArgs>? ValidationCompleted;

        /// <summary>
        /// Validiert einen Wert im gegebenen Kontext und gibt eine StateMachine zurück,
        /// die visuelle Reaktionen steuert.
        /// </summary>
        /// <param name="context">Der Validierungskontext mit Wert und Metadaten.</param>
        /// <param name="elementId">Optionale ID des UI-Elements für StateMachine-Tracking.</param>
        /// <returns>Tuple aus ValidationResult und ValidationStateMachine.</returns>
        public async Task<(ValidationResult Result, ValidationStateMachine StateMachine)> ValidateAsync(
            ValidationContext context, string? elementId = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var key = GetKey(context);
            elementId ??= context.RequestId.ToString();

            // StateMachine holen oder erstellen
            var stateMachine = _activeStateMachines.GetOrAdd(elementId, _ => new ValidationStateMachine());
            
            try
            {
                // Validierung starten
                stateMachine.StartValidation();

                // ML-basierte Validierung durchführen
                var result = await PerformValidationAsync(context, key);
                result.DurationMs = stopwatch.ElapsedMilliseconds;

                // Ergebnis in StateMachine verarbeiten
                stateMachine.ProcessResult(result);

                // Wert zur Historie hinzufügen (für zukünftiges Training)
                AddToHistory(key, context);

                // Event auslösen
                ValidationCompleted?.Invoke(this, new ValidationCompletedEventArgs(result, stateMachine));

                Logger?.Eintragen($"ContextAwareValidation: {context.FieldName} = {context.ValueAsString} -> {result.State} (Confidence: {result.Confidence:P0})");

                return (result, stateMachine);
            }
            catch (Exception ex)
            {
                var errorResult = ValidationResult.Error(context, ex.Message);
                errorResult.DurationMs = stopwatch.ElapsedMilliseconds;
                stateMachine.ProcessResult(errorResult);
                
                ExceptionHandler.Logger?.LogException(ex, "ContextAwareValidationService.ValidateAsync");
                Logger?.LogException(ex, "ContextAwareValidationService.ValidateAsync");
                
                return (errorResult, stateMachine);
            }
        }

        /// <summary>
        /// Führt die eigentliche ML-basierte Validierung durch.
        /// </summary>
        private async Task<ValidationResult> PerformValidationAsync(ValidationContext context, string key)
        {
            return await Task.Run(() =>
            {
                // Statistiken für das Feld abrufen/berechnen
                var stats = GetOrComputeStatistics(key, context);

                if (stats == null || stats.Count < Configuration.MinSamplesForValidation)
                {
                    // Nicht genug Daten für ML-Validierung - Plausibilitätsprüfung
                    return PerformBasicValidation(context);
                }

                var actualValue = context.AsDouble();
                if (!actualValue.HasValue)
                {
                    return ValidationResult.Success(context, 0.5, "Nicht-numerischer Wert - Basis-Validierung");
                }

                // Z-Score berechnen (wie viele Standardabweichungen vom Mittelwert)
                var zScore = Math.Abs((actualValue.Value - stats.Mean) / stats.StdDev);

                // Perzentil berechnen
                var percentile = CalculatePercentile(actualValue.Value, stats);

                // Abweichungs-Score basierend auf Z-Score und Perzentil
                var deviationScore = CalculateDeviationScore(zScore, percentile, context, stats);

                // Schwellenwerte basierend auf Konfiguration
                var (warningThreshold, anomalyThreshold) = GetThresholds();

                ValidationResult result;

                if (deviationScore < warningThreshold)
                {
                    result = ValidationResult.Success(context, 1.0 - (deviationScore / anomalyThreshold));
                    result.Summary = $"Wert ist plausibel (Abweichung: {deviationScore:F2})";
                }
                else if (deviationScore < anomalyThreshold)
                {
                    result = ValidationResult.AsWarning(context, deviationScore, stats.Mean,
                        $"Wert weicht um {deviationScore:F1} Standardabweichungen vom Durchschnitt ab.");
                    result.Confidence = 0.7;
                    result.Recommendations.Add($"Erwarteter Bereich: {stats.Mean - stats.StdDev:F2} - {stats.Mean + stats.StdDev:F2}");
                }
                else
                {
                    result = ValidationResult.AsAnomaly(context, deviationScore, stats.Mean,
                        $"Wert weicht stark ab! Z-Score: {zScore:F2}, Perzentil: {percentile:F1}%");
                    result.Confidence = 0.9;
                    result.Recommendations.Add($"Typischer Bereich: {stats.Min:F2} - {stats.Max:F2}");
                    result.Recommendations.Add($"Durchschnitt: {stats.Mean:F2}");
                }

                // Ähnliche historische Werte hinzufügen
                result.SimilarValues = FindSimilarValues(actualValue.Value, key, 3);

                return result;
            });
        }

        /// <summary>
        /// Führt eine einfache Plausibilitätsprüfung durch, wenn nicht genug historische Daten vorhanden sind.
        /// </summary>
        private ValidationResult PerformBasicValidation(ValidationContext context)
        {
            var value = context.AsDouble();

            // Prüfe definierte Grenzen
            if (context.ExpectedMin.HasValue && value < context.ExpectedMin)
            {
                return ValidationResult.AsAnomaly(context, 3.0, context.ExpectedMin,
                    $"Wert liegt unter dem erwarteten Minimum von {context.ExpectedMin}");
            }

            if (context.ExpectedMax.HasValue && value > context.ExpectedMax)
            {
                return ValidationResult.AsAnomaly(context, 3.0, context.ExpectedMax,
                    $"Wert liegt über dem erwarteten Maximum von {context.ExpectedMax}");
            }

            // Basis-Plausibilität für numerische Werte
            if (value.HasValue)
            {
                // Negative Werte bei typischerweise positiven Feldern
                if (IsTypicallyPositiveField(context.FieldName) && value < 0)
                {
                    return ValidationResult.AsWarning(context, 2.0, 0,
                        "Negativer Wert in einem typischerweise positiven Feld");
                }
            }

            return ValidationResult.Success(context, 0.5, "Basis-Validierung bestanden (wenig historische Daten)");
        }

        /// <summary>
        /// Berechnet den Abweichungs-Score basierend auf mehreren Faktoren.
        /// </summary>
        private double CalculateDeviationScore(double zScore, double percentile, 
            ValidationContext context, FieldStatistics stats)
        {
            var score = zScore;

            // Extreme Perzentile verstärken den Score
            if (percentile < 5 || percentile > 95)
            {
                score *= 1.2;
            }

            // Kontext-spezifische Anpassungen
            if (context.RelatedValues.Any())
            {
                // Wenn verwandte Werte vorhanden sind, prüfe Konsistenz
                var consistency = CheckConsistencyWithRelatedValues(context, stats);
                if (consistency < 0.5)
                {
                    score *= 1.3; // Inkonsistenz erhöht den Score
                }
            }

            return score;
        }

        /// <summary>
        /// Prüft Konsistenz mit verwandten Werten.
        /// </summary>
        private double CheckConsistencyWithRelatedValues(ValidationContext context, FieldStatistics stats)
        {
            // Placeholder für erweiterte Konsistenzprüfung
            return 1.0;
        }

        /// <summary>
        /// Gibt Schwellenwerte basierend auf der Konfiguration zurück.
        /// </summary>
        private (double warning, double anomaly) GetThresholds()
        {
            return Configuration.Threshold switch
            {
                AnomalyThreshold.Strict => (1.5, 2.5),
                AnomalyThreshold.Normal => (2.0, 3.0),
                AnomalyThreshold.Tolerant => (2.5, 4.0),
                _ => (2.0, 3.0)
            };
        }

        /// <summary>
        /// Berechnet das Perzentil eines Wertes.
        /// </summary>
        private double CalculatePercentile(double value, FieldStatistics stats)
        {
            if (stats.SortedValues == null || stats.SortedValues.Count == 0)
                return 50;

            int count = stats.SortedValues.Count(v => v < value);
            return (double)count / stats.SortedValues.Count * 100;
        }

        /// <summary>
        /// Findet ähnliche historische Werte.
        /// </summary>
        private List<SimilarValueInfo> FindSimilarValues(double value, string key, int count)
        {
            if (!_valueHistory.TryGetValue(key, out var history))
                return new List<SimilarValueInfo>();

            return history
                .Where(h => h.NumericValue.HasValue)
                .Select(h => new
                {
                    Entry = h,
                    Distance = Math.Abs(h.NumericValue!.Value - value)
                })
                .OrderBy(x => x.Distance)
                .Take(count)
                .Select(x => new SimilarValueInfo
                {
                    Value = x.Entry.NumericValue!.Value,
                    Date = x.Entry.Timestamp,
                    Similarity = 1.0 / (1.0 + x.Distance),
                    Description = x.Entry.Context?.EntityName
                })
                .ToList();
        }

        /// <summary>
        /// Holt oder berechnet Statistiken für ein Feld.
        /// </summary>
        private FieldStatistics? GetOrComputeStatistics(string key, ValidationContext context)
        {
            if (_fieldStatistics.TryGetValue(key, out var cached) && 
                (DateTime.Now - cached.ComputedAt).TotalMinutes < Configuration.StatsCacheMinutes)
            {
                return cached;
            }

            if (!_valueHistory.TryGetValue(key, out var history) || history.Count < 3)
            {
                return null;
            }

            var values = history
                .Where(h => h.NumericValue.HasValue)
                .Select(h => h.NumericValue!.Value)
                .ToList();

            if (values.Count < 3)
                return null;

            var stats = new FieldStatistics
            {
                Key = key,
                Count = values.Count,
                Mean = values.Average(),
                Min = values.Min(),
                Max = values.Max(),
                StdDev = CalculateStdDev(values),
                SortedValues = values.OrderBy(v => v).ToList(),
                ComputedAt = DateTime.Now
            };

            _fieldStatistics[key] = stats;
            return stats;
        }

        /// <summary>
        /// Berechnet die Standardabweichung.
        /// </summary>
        private double CalculateStdDev(List<double> values)
        {
            var mean = values.Average();
            var sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / values.Count);
        }

        /// <summary>
        /// Fügt einen Wert zur Historie hinzu.
        /// </summary>
        private void AddToHistory(string key, ValidationContext context)
        {
            var history = _valueHistory.GetOrAdd(key, _ => new List<HistoricalValueEntry>());
            
            lock (history)
            {
                history.Add(new HistoricalValueEntry
                {
                    Timestamp = context.Timestamp,
                    NumericValue = context.AsDouble(),
                    StringValue = context.ValueAsString,
                    Context = context
                });

                // Begrenze Historie-Größe
                while (history.Count > Configuration.MaxHistoryPerField)
                {
                    history.RemoveAt(0);
                }
            }

            // Statistik-Cache invalidieren
            _fieldStatistics.TryRemove(key, out _);
        }

        /// <summary>
        /// Generiert einen Schlüssel für die Historie basierend auf Kontext.
        /// </summary>
        private string GetKey(ValidationContext context)
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(context.Category))
                parts.Add(context.Category);
            if (!string.IsNullOrEmpty(context.ViewName))
                parts.Add(context.ViewName);
            if (!string.IsNullOrEmpty(context.EntityName))
                parts.Add(context.EntityName);
            if (!string.IsNullOrEmpty(context.FieldName))
                parts.Add(context.FieldName);

            return parts.Count > 0 ? string.Join(":", parts) : "default";
        }

        /// <summary>
        /// Prüft ob ein Feld typischerweise positive Werte hat.
        /// </summary>
        private bool IsTypicallyPositiveField(string? fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return false;

            var positiveIndicators = new[] { "preis", "price", "menge", "quantity", "anzahl", "count", "betrag", "amount" };
            return positiveIndicators.Any(i => fieldName.Contains(i, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Holt eine aktive StateMachine für ein Element.
        /// </summary>
        public ValidationStateMachine? GetStateMachine(string elementId)
        {
            _activeStateMachines.TryGetValue(elementId, out var sm);
            return sm;
        }

        /// <summary>
        /// Entfernt eine StateMachine.
        /// </summary>
        public bool RemoveStateMachine(string elementId)
        {
            return _activeStateMachines.TryRemove(elementId, out _);
        }

        /// <summary>
        /// Lädt historische Daten für ein Feld (für initiales Training).
        /// </summary>
        public void LoadHistoricalData(string category, string fieldName, IEnumerable<(DateTime date, double value)> data)
        {
            var key = $"{category}:{fieldName}";
            var history = _valueHistory.GetOrAdd(key, _ => new List<HistoricalValueEntry>());

            lock (history)
            {
                foreach (var (date, value) in data)
                {
                    history.Add(new HistoricalValueEntry
                    {
                        Timestamp = date,
                        NumericValue = value,
                        StringValue = value.ToString()
                    });
                }
            }

            _fieldStatistics.TryRemove(key, out _);
            Logger?.Eintragen($"ContextAwareValidation: {data.Count()} historische Werte für {key} geladen.");
        }

        /// <summary>
        /// Leert alle gecachten Daten.
        /// </summary>
        public void ClearCache()
        {
            _valueHistory.Clear();
            _fieldStatistics.Clear();
            _activeStateMachines.Clear();
        }
    }

    /// <summary>
    /// Konfiguration für Anomalie-Erkennung.
    /// </summary>
    public class AnomalyDetectionConfiguration
    {
        /// <summary>
        /// Schwellenwert-Empfindlichkeit.
        /// </summary>
        public AnomalyThreshold Threshold { get; set; } = AnomalyThreshold.Normal;

        /// <summary>
        /// Minimale Anzahl an Samples für ML-Validierung.
        /// </summary>
        public int MinSamplesForValidation { get; set; } = 10;

        /// <summary>
        /// Maximale Historie pro Feld.
        /// </summary>
        public int MaxHistoryPerField { get; set; } = 1000;

        /// <summary>
        /// Cache-Dauer für Statistiken in Minuten.
        /// </summary>
        public int StatsCacheMinutes { get; set; } = 5;
    }

    /// <summary>
    /// Historischer Wert-Eintrag.
    /// </summary>
    internal class HistoricalValueEntry
    {
        public DateTime Timestamp { get; set; }
        public double? NumericValue { get; set; }
        public string? StringValue { get; set; }
        public ValidationContext? Context { get; set; }
    }

    /// <summary>
    /// Statistiken für ein Feld.
    /// </summary>
    internal class FieldStatistics
    {
        public string Key { get; set; } = "";
        public int Count { get; set; }
        public double Mean { get; set; }
        public double StdDev { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public List<double>? SortedValues { get; set; }
        public DateTime ComputedAt { get; set; }
    }

    /// <summary>
    /// EventArgs für abgeschlossene Validierung.
    /// </summary>
    public class ValidationCompletedEventArgs : EventArgs
    {
        public ValidationResult Result { get; }
        public ValidationStateMachine StateMachine { get; }

        public ValidationCompletedEventArgs(ValidationResult result, ValidationStateMachine stateMachine)
        {
            Result = result;
            StateMachine = stateMachine;
        }
    }
}
