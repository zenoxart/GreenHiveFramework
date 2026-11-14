# GreenHive.Wpf Framework

Eine umfassende WPF-Framework-Lösung für moderne .NET 10-Anwendungen mit erweiterten Funktionen für Enterprise-Applikationen.

## ?? Überblick

Das GreenHive.Wpf Framework ist eine modulare WPF-Bibliothek, die eine umfangreiche Sammlung von Komponenten, Services und Patterns für die Entwicklung professioneller Desktop-Anwendungen bereitstellt. Das Framework folgt dem MVVM-Pattern und bietet zahlreiche Funktionen für moderne Unternehmensanwendungen.

## ??? Projektstruktur

Das Framework besteht aus fünf Hauptprojekten:

### **GreenHive.Wpf.UI** (Hauptanwendung)
Das Haupt-Einstiegsprojekt, das die Framework-Komponenten integriert und demonstriert.
- **Zielframework:** .NET 10 (net10.0-windows10.0.19041.0)
- **Typ:** WPF-Anwendung

### **GreenHive.Wpf.Core** (Kernbibliothek)
Die Kernbibliothek mit allen wesentlichen Services, Patterns und Basisklassen:

#### MVVM-Komponenten
- `NotificationObject` - Basisklasse für INotifyPropertyChanged
- `ModelBase` - Basis für Domain-Modelle
- `ViewModelLocator` - ViewModel-Auflösung
- `CompositeModuleHandler` - Modulare Anwendungskomponenten

#### Services
- **ClipboardService** - Zwischenablagenverwaltung
- **DialogService** - Dialog-Management
- **FileDialogService** - Dateidialog-Handling
- **NotificationService** - Benachrichtigungssystem
- **SettingsService** - Einstellungsverwaltung
- **BusyIndicatorService** - Ladeanzeigen
- **ResourceService** - Ressourcenverwaltung
- **TelemetryService** - Telemetrie und Analytics

#### Dependency Injection / IoC
- `AppKontext` - Anwendungskontext und DI-Container
- `AppObject` / `IAppObject` - Basis für DI-fähige Objekte
- `AppLogManager` - Logging-Management
- `AppSprachenManager` - Mehrsprachigkeit

#### Command-System
- `RelayCommand` - Standard WPF Commands
- `AsyncRelayCommand` - Asynchrone Commands
- `RelayCommandPool` - Command-Pooling für Performance
- `RelayMenu` / `RelayObject` - Menu-Commands
- `MenuItemModel` - Menu-Modelle

#### Command-Features
- **CommandPaletteItem** / **CommandPaletteViewModel** - VS Code-ähnliche Command-Palette
- **CommandReplayManager** / **RecordedCommand** - Command-Aufzeichnung und Wiedergabe

#### Hintergrund-Tasks
- `BackgroundTaskScheduler` - Task-Scheduler
- `BackgroundTask` - Basis für Background-Tasks
- `TaskPriority` - Priorisierung

#### Threading & Events
- `DispatcherHelper` - UI-Thread-Synchronisation
- `EventAggregator` - Lose gekoppelte Events
- `DatagridProgress` - Fortschrittsanzeige in DataGrids

#### Workflow & State Management
- `StateMachine` - State-Machine-Implementation
- `StateSnapshot` / `SnapshotManager` - Anwendungszustände speichern/wiederherstellen

#### Netzwerk & Verbindungen
- `APIService` - REST-API-Client
- `GraphQLService` - GraphQL-Client
- `RedisService` - Redis-Integration
- `UDPService` - UDP-Kommunikation
- `ConnectionPool` - Verbindungs-Pooling

#### Machine Learning
- `MLService` - ML.NET-Integration

#### Sicherheit & Speicher
- `SecureStorage` - Sichere Datenspeicherung
- `PermissionManager` - Rechteverwaltung
- `RequiresPermissionAttribute` - Attribut für Berechtigungen

#### Tutorials & Hilfe
- `TutorialService` / `TutorialManager` - Interaktive Tutorials
- `TutorialStep` / `TutorialPopup` / `TutorialHelper` - Tutorial-Komponenten
- `TutorialXmlLoader` - XML-basierte Tutorial-Konfiguration

#### Updates & Recovery
- `AutoUpdateService` - Automatische Updates
- `UpdateModels` - Update-Modelle
- `CrashRecoveryManager` - Crash-Recovery und Wiederherstellung

#### Neuigkeiten
- `NeuigkeitenLoader` / `NeuigkeitenModels` - "Was ist neu"-Feature

#### Konverter
- `TimeToDecimalConverter` - Zeit zu Dezimal
- `TimeToDecimalMinusWorkBreakConverter` - Zeit mit Pausen
- `SvgPathToGeometryConverter` - SVG zu WPF-Geometrie

#### Extensions & Utilities
- `FontMapper` / `FontIconExtension` / `FontSource` - Icon-Font-Integration
- `GitTag` - Git-Tag-Utilities
- `GlobalKeyboardHandler` - Globale Tastaturkürzel
- `PowerUserMode` - Erweiterte Features für Power-User

#### Strategies
- `IStrategy` / `StrategyProvider` - Strategy-Pattern-Implementation

#### Controller
- `XmlController` - XML-Verarbeitung
- `SprachenXmlController` - Sprachdateien

#### Annotationen
- `AutoDisposeAttribute` - Automatisches Dispose
- `ThreadSafeAttribute` - Thread-Sicherheit markieren
- `UIThreadOnlyAttribute` / `BackgroundThreadOnlyAttribute` - Thread-Affinität
- `ImmutableAttribute` - Unveränderlichkeit

#### Exception Handling
- `ExceptionHandler` - Globale Exception-Behandlung
- `MainRootNavigationNotInitializedException` - Custom Exceptions

### **GreenHive.Wpf.Controls** (UI-Steuerelemente)
Wiederverwendbare WPF-Steuerelemente:

#### Tab-System
- `TabViewControl` - Erweiterte Tab-Verwaltung
- `TabItemRemovedEventArgs` - Tab-Events
- Konverter: `PinMenuHeaderConverter`, `PinTooltipConverter`, `PinIconConverter`, `InverseBoolConverter`

#### Menüs
- `LeftMenuControl` - Seitenmenü
- `TopMenuControl` - Top-Navigation

#### Command-Features
- `CommandPaletteWindow` / `CommandPaletteManager` - Command-Palette UI

#### Monitoring & Debugging
- `PerformanceOverlay` - Performance-Überwachung
- `ExecutionLogFenster` - Execution-Log-Fenster
- `CommandHistoryWindow` - Command-Historie

#### Benachrichtigungen
- `NotificationContainer` - Benachrichtigungs-Container
- `NotificationToast` - Toast-Benachrichtigungen

#### Updates & Recovery
- `UpdateNotificationDialog` - Update-Benachrichtigung
- `CrashRecoveryDialog` - Crash-Recovery-Dialog

#### State Management
- `SnapshotManagerWindow` - Snapshot-Verwaltungsfenster

### **GreenHive.Wpf.Views** (Ansichten)
XAML-Views und ViewModels:

- `DashboardView` - Dashboard
- `AdminView` - Admin-Bereich
- `EinstellungenView` - Einstellungen
- `DefaultView` - Standardansicht
- `AboutView` - Über-Dialog
- `NeuigkeitenView` / `NeuigkeitenViewModel` - Neuigkeiten

### **GreenHive.Wpf.Dto** (Data Transfer Objects)
Datenmodelle und DTOs:

- `CommandRecord` - Befehl mit Name und Ausführungszeit
- `TestRecord` - Beispiel-DTO

## ?? Hauptfunktionen

### 1. **Modulare Architektur**
- Klare Trennung in Core, Controls, Views und Dto
- Plugin-fähige Architektur durch `CompositeModuleHandler`

### 2. **MVVM-Pattern**
- Vollständige MVVM-Unterstützung
- ViewModelLocator für automatische ViewModel-Auflösung
- NotificationObject als Basis

### 3. **Command-System**
- Synchrone und asynchrone Commands
- Command-Pooling für Performance
- Command-Palette (VS Code-Style)
- Command-Recording und Replay

### 4. **Background-Tasks**
- Task-Scheduler mit Prioritäten
- Async/Await-Unterstützung
- Task-Überwachung

### 5. **State Management**
- Snapshots für Anwendungszustände
- Undo/Redo-Funktionalität
- Crash-Recovery

### 6. **Benachrichtigungssystem**
- Toast-Benachrichtigungen
- In-App-Benachrichtigungen
- Notification-Center

### 7. **Tutorial-System**
- Interaktive Tutorials
- XML-basierte Konfiguration
- Step-by-Step-Guides

### 8. **Performance-Monitoring**
- Performance-Overlay
- Execution-Logging
- Telemetrie

### 9. **Netzwerk & APIs**
- REST-API-Client
- GraphQL-Support
- Redis-Integration
- Connection-Pooling

### 10. **Sicherheit**
- SecureStorage für sensible Daten
- Permission-Management
- Attribut-basierte Autorisierung

## ?? Technologie-Stack

- **.NET Version:** 10.0
- **C# Version:** 14.0 (Latest)
- **UI-Framework:** WPF (Windows Presentation Foundation)
- **Target OS:** Windows 10 (19041.0) und höher
- **Pattern:** MVVM, Strategy, Factory, Observer

## ?? Installation & Setup

### Voraussetzungen
- .NET 10 SDK
- Windows 10 (Build 19041) oder höher
- Visual Studio 2022 oder höher

### Build
```bash
dotnet build GreenHive.Wpf.UI.csproj
```

### Ausführen
```bash
dotnet run --project GreenHive.Wpf.UI.csproj
```

## ?? Verwendung

### Beispiel: Einfaches Command erstellen
```csharp
using GreenHive.NET.Core.Common.Command;

public class MyViewModel : NotificationObject
{
    public RelayCommand MyCommand { get; }

    public MyViewModel()
    {
        MyCommand = new RelayCommand(ExecuteMyCommand, CanExecuteMyCommand);
    }

    private void ExecuteMyCommand(object? parameter)
    {
        // Command-Logik
    }

    private bool CanExecuteMyCommand(object? parameter)
    {
        return true;
    }
}
```

### Beispiel: Asynchrones Command
```csharp
using GreenHive.NET.Core.Common.Command;

public class MyViewModel : NotificationObject
{
    public AsyncRelayCommand LoadDataCommand { get; }

    public MyViewModel()
    {
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
    }

    private async Task LoadDataAsync()
    {
        // Asynchrone Datenladung
        await Task.Delay(1000);
    }
}
```

### Beispiel: Notification senden
```csharp
using GreenHive.NET.Core.Common.Notifications;

NotificationService.Instance.Show(
    "Titel",
    "Nachricht",
    NotificationType.Information
);
```

### Beispiel: Background-Task planen
```csharp
using GreenHive.NET.Core.Common.BackgroundTasks;

var task = new BackgroundTask("MyTask", async () =>
{
    // Task-Logik
    await Task.Delay(1000);
}, TaskPriority.Normal);

BackgroundTaskScheduler.Instance.Schedule(task);
```

## ??? Architektur-Prinzipien

1. **Separation of Concerns** - Klare Trennung zwischen UI, Logik und Daten
2. **Dependency Injection** - Lose Kopplung durch IoC-Container
3. **Single Responsibility** - Jede Klasse hat eine klare Verantwortlichkeit
4. **Open/Closed Principle** - Erweiterbar ohne Änderung
5. **Interface Segregation** - Kleine, fokussierte Interfaces

## ?? Command-Replay-Feature

Das Framework bietet ein leistungsstarkes Command-Replay-System für Automatisierung, Testing und Debugging. Der `CommandReplayManager` zeichnet Benutzer-Commands auf und ermöglicht deren Wiedergabe.

### Hauptfunktionen

- **Automatische Aufzeichnung** - Commands werden automatisch mit Zeitstempel erfasst
- **Kategorie-basierte Organisation** - Commands nach Kategorien gruppieren
- **Zeitbasierte Suche** - Historie nach Zeitraum filtern
- **Sequenz-Persistierung** - Command-Sequenzen speichern und laden
- **Performance-Tracking** - Ausführungszeiten messen
- **Event-System** - Events für Aufzeichnung und Wiedergabe

### Grundlegende Verwendung

#### Command aufzeichnen
```csharp
using GreenHive.NET.Core.Common.CommandReplay;

// Einfaches Command aufzeichnen
var recordedCommand = CommandReplayManager.Instance.RecordCommand(
    name: "SaveDocument",
    parameters: new { DocumentId = 123 },
    category: "Document"
);

// Command mit Timing aufzeichnen
await CommandReplayManager.Instance.RecordCommandAsync(
    name: "LoadData",
    action: async () => {
        await LoadDataFromDatabaseAsync();
    },
    parameters: new { Filter = "Active" },
    category: "Data"
);
```

#### Command-Handler registrieren
```csharp
// Handler für Replay registrieren
CommandReplayManager.Instance.RegisterHandler("SaveDocument", async (parametersJson) => {
    // Parameter deserialisieren
    var parameters = JsonSerializer.Deserialize<SaveDocumentParams>(parametersJson);
    
    // Command ausführen
    await SaveDocumentAsync(parameters.DocumentId);
    
    return true; // Erfolg
});
```

#### Command wiederholen
```csharp
// Einzelnes Command wiederholen
var history = CommandReplayManager.Instance.GetHistory(maxCount: 10);
var lastCommand = history.FirstOrDefault();
if (lastCommand != null)
{
    bool success = await CommandReplayManager.Instance.ReplayCommand(lastCommand);
}

// Sequenz wiederholen
var recentCommands = CommandReplayManager.Instance.GetHistory(maxCount: 5);
var result = await CommandReplayManager.Instance.ReplaySequence(recentCommands);

Console.WriteLine($"Replayed {result.TotalCommands} commands");
Console.WriteLine($"Success: {result.SuccessfulCommands}, Failed: {result.FailedCommands}");
Console.WriteLine($"Duration: {result.DurationMs}ms");
```

### Historie verwalten

```csharp
// Alle Commands abrufen
var allCommands = CommandReplayManager.Instance.GetHistory(maxCount: 100);

// Nach Kategorie filtern
var documentCommands = CommandReplayManager.Instance.GetHistoryByCategory(
    category: "Document",
    maxCount: 50
);

// Nach Zeitraum filtern
var today = DateTime.Today;
var todaysCommands = CommandReplayManager.Instance.GetHistoryByTimeRange(
    from: today,
    to: today.AddDays(1)
);

// Historie löschen
CommandReplayManager.Instance.ClearHistory();
```

### Sequenzen speichern und laden

```csharp
// Sequenz speichern
var commands = CommandReplayManager.Instance.GetHistoryByCategory("Document");
CommandReplayManager.Instance.SaveSequence(commands, "DocumentWorkflow");

// Gespeicherte Sequenzen auflisten
var savedSequences = CommandReplayManager.Instance.GetSavedSequences();
foreach (var sequenceName in savedSequences)
{
    Console.WriteLine($"Saved sequence: {sequenceName}");
}

// Sequenz laden und wiederholen
var sequence = CommandReplayManager.Instance.LoadSequence("DocumentWorkflow");
if (sequence != null)
{
    var result = await CommandReplayManager.Instance.ReplaySequence(sequence.Commands);
}
```

### Events

```csharp
// Events abonnieren
CommandReplayManager.Instance.CommandRecorded += (sender, command) => {
    Console.WriteLine($"Command recorded: {command.Name} at {command.Timestamp}");
};

CommandReplayManager.Instance.CommandReplayed += (sender, command) => {
    Console.WriteLine($"Command replayed: {command.Name}");
};

CommandReplayManager.Instance.ReplaySequenceStarted += (sender, sequenceId) => {
    Console.WriteLine($"Replay sequence started: {sequenceId}");
};

CommandReplayManager.Instance.ReplaySequenceCompleted += (sender, args) => {
    var result = args.Result;
    Console.WriteLine($"Sequence completed: {result.SuccessfulCommands}/{result.TotalCommands} successful");
};
```

### Aufzeichnung steuern

```csharp
// Aufzeichnung deaktivieren
CommandReplayManager.Instance.IsRecording = false;

// Commands werden jetzt nicht mehr aufgezeichnet
var cmd = CommandReplayManager.Instance.RecordCommand("Test"); // Returns null

// Aufzeichnung wieder aktivieren
CommandReplayManager.Instance.IsRecording = true;
```

### Anwendungsfälle

1. **Automatisiertes Testing** - Benutzer-Workflows aufzeichnen und automatisiert wiederholen
2. **Debugging** - Fehlerhafte Sequenzen reproduzieren
3. **Performance-Analyse** - Command-Ausführungszeiten analysieren
4. **User-Training** - Workflows als Tutorials abspielen
5. **Macro-Recording** - Wiederkehrende Aufgaben automatisieren

### Integration in ViewModels

```csharp
public class DocumentViewModel : NotificationObject
{
    public RelayCommand SaveCommand { get; }

    public DocumentViewModel()
    {
        SaveCommand = new RelayCommand(async (param) => {
            await CommandReplayManager.Instance.RecordCommandAsync(
                "SaveDocument",
                async () => await SaveDocumentAsync(),
                parameters: new { DocumentId = CurrentDocument.Id },
                category: "Document"
            );
        });
        
        // Handler registrieren
        CommandReplayManager.Instance.RegisterHandler("SaveDocument", 
            async (json) => await HandleSaveCommand(json));
    }
}
```

## ?? Performance-Features

- **Command-Pooling** - Reduziert GC-Pressure
- **Connection-Pooling** - Effiziente Netzwerkverbindungen
- **Lazy Loading** - ViewModels werden bei Bedarf geladen
- **Background-Threading** - UI bleibt responsive

## ?? Sicherheits-Features

- **SecureStorage** - Verschlüsselte Datenspeicherung
- **Permission-System** - Rollenbasierte Zugriffskontrolle
- **Crash-Recovery** - Automatische Wiederherstellung
- **Telemetrie** - Opt-in-basiert

## ?? Internationalisierung

Das Framework unterstützt Mehrsprachigkeit:

- `AppSprachenManager` - Sprachverwaltung
- `SprachenXmlController` - XML-basierte Sprachdateien
- Dynamischer Sprachwechsel zur Laufzeit

## ?? Lizenz

[Lizenzinformationen hier einfügen]

## ?? Beiträge

Beiträge sind willkommen! Bitte beachten Sie die Coding-Standards:
- C# 14.0 Features verwenden
- XML-Dokumentation für öffentliche APIs
- Unit-Tests für neue Features
- MVVM-Pattern einhalten

## ?? Kontakt

[Kontaktinformationen hier einfügen]

## ??? Roadmap

- [ ] Weitere UI-Controls
- [ ] Erweiterte ML-Integration
- [ ] Cloud-Integration
- [ ] Plugin-Marketplace

---

**GreenHive.Wpf Framework** - Ein modernes WPF-Framework für Enterprise-Anwendungen
