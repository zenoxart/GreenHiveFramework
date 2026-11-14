# Context-Aware Validation System

Ein einfaches, praktikabler System für AI-gestützte Validierung von Werten in WPF-Anwendungen.

## Features

- ? **Einfache Integration** - Ein Behavior, fertig
- ? **Automatische Erkennung** - Erkennt automatisch Werte aus UI-Elementen
- ? **Visuelles Feedback** - Border-Highlighting, Pulse-Animation, Toast-Benachrichtigungen
- ? **Kontextmenü-Integration** - Rechtsklick auf beliebiges Element
- ? **Globale Aktivierung** - Optional für alle Fenster
- ? **Anpassbar** - Feedback-Optionen, Sensitivität konfigurierbar

## Schnellstart

### 1. Einfache Nutzung per Behavior

```xml
<TextBox Text="{Binding Preis}"
         local:ValidationBehavior.IsEnabled="True"
         local:ValidationBehavior.FieldName="Preis" />
```

Fertig! Rechtsklick auf die TextBox zeigt "?? AI Validierung" im Kontextmenü.

### 2. Mit benutzerdefinierten Feedback-Optionen

```xml
<TextBox Text="{Binding Menge}"
         local:ValidationBehavior.IsEnabled="True"
         local:ValidationBehavior.FeedbackOptions="BorderHighlight, Pulse" />
```

### 3. Globale Aktivierung für alle Fenster

```csharp
// In App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // Aktiviere AI-Validierung für alle TextBoxen, Labels, etc.
    GlobalValidationManager.Instance.Initialize();
}
```

### 4. Programmatische Validierung

```csharp
// Einfache Validierung
var result = await ValidationService.Instance.ValidateValueAsync(
    value: 150.50,
    fieldName: "Preis",
    element: myTextBox  // Optional für visuelles Feedback
);

if (result.State == ValidationState.Anomaly)
{
    Console.WriteLine($"Anomalie erkannt: {result.Message}");
}
```

### 5. Mit vollem Kontext

```csharp
var context = new ValidationContext
{
    Value = 999.99,
    FieldName = "Preis",
    ExpectedMin = 50,
    ExpectedMax = 500,
    HistoricalValues = new List<object?> { 100.0, 120.0, 95.5 }
};

var result = await ValidationService.Instance.ValidateAsync(
    context,
    myTextBox,
    ValidationFeedbackOptions.Default
);
```

## Konfiguration

### Feedback-Optionen

```csharp
[Flags]
public enum ValidationFeedbackOptions
{
    None = 0,                           // Kein Feedback
    BorderHighlight = 1,                // Border wird farbig
    Toast = 2,                          // Toast-Benachrichtigung
    Pulse = 4,                          // Pulse-Animation
    Default = BorderHighlight | Toast   // Standard-Kombination
}
```

### Sensitivität

```csharp
// Globale Einstellung
ValidationService.Instance.Sensitivity = AnomalySensitivity.High;

public enum AnomalySensitivity
{
    Low = 30,      // 30% Abweichung = Anomalie
    Normal = 20,   // 20% Abweichung = Anomalie (Standard)
    High = 10      // 10% Abweichung = Anomalie
}
```

### Feedback anpassen

```csharp
// Standard-Feedback für alle Validierungen
ValidationService.Instance.DefaultFeedback = 
    ValidationFeedbackOptions.BorderHighlight | 
    ValidationFeedbackOptions.Pulse;
```

## Architektur

### Komponenten

```
ValidationService          ? Zentrale API für Validierung + Feedback
??? ValidationContext      ? Kontext-Daten für Validierung
??? ValidationResult       ? Ergebnis der Validierung
??? ContextExtractor       ? Extrahiert automatisch Kontext aus UI

ValidationBehavior         ? Attached Behavior für XAML
GlobalValidationManager    ? Optional: Automatische globale Aktivierung
```

### Ablauf

1. **Benutzer** macht Rechtsklick ? Kontextmenü
2. **ValidationBehavior** fängt Klick ab
3. **ContextExtractor** sammelt Informationen (Wert, ViewModel, Historie, etc.)
4. **ValidationService** führt Validierung durch
5. **ValidationService** zeigt Feedback (Border, Toast, etc.)

## Validierungsergebnisse

### ValidationState

- **Valid** - Wert ist plausibel ?
- **Warning** - Leichte Abweichung (< 10-20%) ??
- **Anomaly** - Starke Abweichung (> 20-30%) ??
- **Error** - Technischer Fehler ?

### ValidationResult

```csharp
public class ValidationResult
{
    public ValidationState State { get; set; }
    public double? ExpectedValue { get; set; }
    public double? ActualValue { get; set; }
    public double? DeviationPercentage { get; set; }
    public string? Message { get; set; }
    public string? Details { get; set; }
    public List<string> Recommendations { get; set; }
    public ValidationSeverity Severity { get; }
}
```

## Beispiele

### Preis-Validierung

```xml
<StackPanel>
    <Label Content="Preis:" />
    <TextBox Text="{Binding Preis}"
             local:ValidationBehavior.IsEnabled="True"
             local:ValidationBehavior.FieldName="Preis" />
</StackPanel>
```

- Rechtsklick ? "?? AI Validierung"
- System vergleicht mit historischen Preisen
- Zeigt Border + Toast bei Abweichung

### Nur Border-Highlight (kein Toast)

```xml
<TextBox Text="{Binding Menge}"
         local:ValidationBehavior.IsEnabled="True"
         local:ValidationBehavior.FeedbackOptions="BorderHighlight" />
```

### Globale Aktivierung mit Filterung

```csharp
var manager = GlobalValidationManager.Instance;

// Nur TextBoxen überwachen
manager.MonitoredTypes.Clear();
manager.MonitoredTypes.Add(typeof(TextBox));

manager.Initialize();
```

## Best Practices

1. **Für numerische Felder**: Immer FieldName angeben für bessere Kontext-Erkennung
2. **Bei wichtigen Feldern**: `FeedbackOptions="Default"` (Border + Toast)
3. **Bei vielen Feldern**: GlobalValidationManager verwenden statt einzelne Behaviors
4. **Mit Historie**: Füge `HistoricalValues` zum Kontext hinzu für bessere Validierung
5. **Fehlerbehandlung**: ValidationResult.State prüfen vor Weiterverarbeitung

## Migration von altem System

Alte Version (kompliziert):
```csharp
var stateMachine = new ValidationStateMachine();
stateMachine.FeedbackRequested += HandleFeedback;
var feedbackManager = new ValidationFeedbackManager();
feedbackManager.ShowFeedback(element, result, stateMachine);
```

Neue Version (einfach):
```csharp
await ValidationService.Instance.ValidateAsync(context, element);
```

## Erweiterte Nutzung

### Custom Validierung

```csharp
public class MyValidator
{
    public async Task<ValidationResult> ValidateCustomAsync(MyModel model)
    {
        var context = new ValidationContext
        {
            Value = model.SpecialValue,
            FieldName = "SpecialValue",
            RelatedValues = new Dictionary<string, object?>
            {
                ["Factor"] = model.Factor,
                ["BaseValue"] = model.BaseValue
            }
        };
        
        return await ValidationService.Instance.ValidateAsync(context);
    }
}
```

### Ohne UI-Element

```csharp
// Validierung ohne visuelles Feedback
var result = await ValidationService.Instance.ValidateAsync(context);

if (result.State == ValidationState.Anomaly)
{
    // Eigene Behandlung
    Logger.Warning($"Anomalie: {result.Message}");
}
```

## Zusammenfassung

**Vorher**: 9 Klassen, StateMachine, komplexe Events, schwer zu integrieren  
**Nachher**: 4 Haupt-Klassen, einfache API, ein Behavior reicht

**Integration**: Von 20+ Zeilen Code auf 1 Zeile XAML reduziert
