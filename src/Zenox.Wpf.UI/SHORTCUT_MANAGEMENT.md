# Shortcut-Management System

Das Shortcut-Management-System ermöglicht die zentrale Verwaltung von Tastaturkürzeln in der Anwendung.

## Features

- ? Generisches System für beliebige Shortcuts
- ? Kategorisierung von Shortcuts
- ? Speichern und Laden von Konfigurationen (JSON)
- ? UI zur Verwaltung von Shortcuts
- ? Konflikt-Erkennung
- ? Reset zu Standardwerten
- ? Import/Export von Konfigurationen
- ? Aktivieren/Deaktivieren einzelner Shortcuts
- ? Live-Bearbeitung mit visueller Rückmeldung
- ? Suchfunktion

## Schnellstart

### 1. Shortcut-Manager initialisieren

In `App.xaml.cs` beim Application_Startup:

```csharp
private void Application_Startup(object sender, StartupEventArgs e)
{
    InitializeShortcutManager();
    // ... andere Initialisierungen
}

private void InitializeShortcutManager()
{
    // Gespeicherte Shortcuts laden
    ShortcutManager.Instance.LoadConfiguration();
    
    // Standard-Shortcuts registrieren
    RegisterDefaultShortcuts();
}
```

### 2. Shortcuts registrieren

```csharp
ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
    id: "save",
    displayName: "Speichern",
    keyGesture: new KeyGesture(Key.S, ModifierKeys.Control),
    action: () => SaveCurrentFile(),
    category: "Datei",
    description: "Speichert die aktuelle Datei"
));
```

### 3. Shortcuts in Window/UserControl verarbeiten

```csharp
private void OnKeyDown(object sender, KeyEventArgs e)
{
    if (ShortcutManager.Instance.HandleKeyPress(e.Key, Keyboard.Modifiers))
    {
        e.Handled = true;
    }
}
```

### 4. Shortcut-Manager UI öffnen

Über das Menü: **Einstellungen > Tastenkürzel verwalten**

Oder programmatisch:
```csharp
var settingsWindow = new Window
{
    Title = "Tastenkürzel verwalten",
    Width = 900,
    Height = 600,
    Content = new Views.ShortcutManagerView(),
    WindowStartupLocation = WindowStartupLocation.CenterScreen
};
settingsWindow.ShowDialog();
```

## Verwendung

### Shortcuts registrieren

```csharp
using Zenox.Wpf.UI.Common.Shortcuts;
using System.Windows.Input;

// Einfacher Shortcut mit Action
ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
    id: "save_file",
    displayName: "Datei speichern",
    keyGesture: new KeyGesture(Key.S, ModifierKeys.Control),
    action: () => SaveCurrentFile(),
    category: "Datei",
    description: "Speichert die aktuell geöffnete Datei"
));

// Shortcut mit Command
ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
    id: "open_settings",
    displayName: "Einstellungen öffnen",
    keyGesture: new KeyGesture(Key.OemComma, ModifierKeys.Control),
    command: OpenSettingsCommand,
    commandParameter: null,
    category: "Navigation",
    description: "Öffnet das Einstellungsfenster"
));

// Shortcut ohne Standard-Tastenkombination
ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
    id: "custom_action",
    displayName: "Meine Aktion",
    keyGesture: null, // Benutzer muss selbst zuweisen
    action: () => DoSomething(),
    category: "Benutzerdefiniert"
));

// Nicht-modifizierbarer System-Shortcut
var systemShortcut = new ShortcutAction(
    id: "emergency_exit",
    displayName: "Notfall-Beenden",
    keyGesture: new KeyGesture(Key.F4, ModifierKeys.Alt),
    action: () => Application.Current.Shutdown(),
    category: "System"
);
systemShortcut.IsModifiable = false;
ShortcutManager.Instance.RegisterShortcut(systemShortcut);
```

### Shortcuts in Ihrer Anwendung verwenden

Im KeyDown-Event-Handler:

```csharp
private void OnKeyDown(object sender, KeyEventArgs e)
{
    // ShortcutManager prüft automatisch alle registrierten Shortcuts
    if (ShortcutManager.Instance.HandleKeyPress(e.Key, Keyboard.Modifiers))
    {
        e.Handled = true; // Event als behandelt markieren
        return;
    }
    
    // Fallback für andere Tastatur-Events
    // ...
}
```

### Konfiguration laden/speichern

```csharp
// Beim Start der Anwendung (in Application_Startup)
ShortcutManager.Instance.LoadConfiguration();

// Beim Beenden oder nach Änderungen (in OnExit oder nach Benutzeränderungen)
ShortcutManager.Instance.SaveConfiguration();
```

Die Konfiguration wird automatisch gespeichert in:
```
%APPDATA%\Zenox\shortcuts.json
```

## UI-Funktionen

### Shortcut-Manager-Oberfläche

Die Shortcut-Manager-View bietet:

1. **Kategorisierte Liste** - Alle Shortcuts gruppiert nach Kategorien
2. **Suchfunktion** - Filtern nach Name, Beschreibung oder Kategorie
3. **Live-Bearbeitung** - Klicken Sie auf ein Tastenkürzel und drücken Sie eine neue Tastenkombination
4. **Konflikt-Erkennung** - Warnung bei bereits verwendeten Tastenkombinationen
5. **Aktivieren/Deaktivieren** - Shortcuts temporär deaktivieren
6. **Reset-Funktionen** - Einzelne oder alle Shortcuts zurücksetzen
7. **Import/Export** - Konfiguration sichern und wiederherstellen

### Shortcuts bearbeiten

1. Öffnen Sie **Einstellungen > Tastenkürzel verwalten**
2. Klicken Sie auf das gewünschte Tastenkürzel
3. Drücken Sie die neue Tastenkombination
4. Drücken Sie **ESC** zum Abbrechen oder **Entf** zum Löschen
5. Klicken Sie auf **Speichern**

## Architektur

### Core-Komponenten

#### ShortcutAction
Repräsentiert einen einzelnen Shortcut mit folgenden Eigenschaften:

- `Id`: Eindeutige Kennung (z.B. "save_file")
- `DisplayName`: Anzeigename (z.B. "Datei speichern")
- `Description`: Beschreibung der Funktion
- `Category`: Kategorie für Gruppierung (z.B. "Datei", "Bearbeiten", "Navigation")
- `KeyGesture`: Die Tastenkombination (z.B. Strg+S)
- `Action`: Auszuführende Funktion (Action)
- `Command`: Auszuführendes Command (ICommand)
- `IsModifiable`: Ob der Benutzer den Shortcut ändern kann
- `IsEnabled`: Ob der Shortcut aktiv ist
- `DefaultKeyGesture`: Original-Tastenkombination (für Reset)

#### ShortcutManager (Singleton)
Zentrale Verwaltung aller Shortcuts:

- **Registrierung**: `RegisterShortcut()`, `UnregisterShortcut()`
- **Abfragen**: `GetShortcut()`, `GetShortcutsByCategory()`, `GetCategories()`
- **Änderung**: `UpdateShortcutKey()`
- **Konflikt-Prüfung**: `IsGestureInUse()`
- **Verarbeitung**: `HandleKeyPress()`
- **Persistenz**: `SaveConfiguration()`, `LoadConfiguration()`
- **Reset**: `ResetToDefault()`, `ResetAllToDefaults()`

#### ShortcutConfiguration
Serialisierbare Konfiguration für Persistierung:

```json
{
  "Version": "1.0",
  "LastModified": "2024-01-15T10:30:00",
  "Shortcuts": [
    {
      "Id": "save",
      "KeyGestureText": "Ctrl+S",
      "IsEnabled": true,
      "Category": "Datei"
    }
  ]
}
```

### UI-Komponenten

#### ShortcutManagerViewModel
- Gruppierung nach Kategorien
- Suchfunktion mit Live-Filterung
- Konflikt-Erkennung in Echtzeit
- Commands: Save, ResetAll, ResetSelected, ClearKey, Export, Import

#### ShortcutManagerView (XAML)
- Übersichtliche kategorisierte Liste
- Inline-Editing mit visueller Rückmeldung
- Statusanzeige für Konflikte
- Aktions-Buttons für alle Funktionen

## Beispiel-Shortcuts

Die Anwendung registriert folgende Standard-Shortcuts:

### Tutorial
- **F1**: Hauptfenster-Tutorial starten
- **F2**: Menü-Navigation-Tutorial starten

### Datei
- **Strg+S**: Speichern

### Navigation
- **Strg+Komma**: Einstellungen öffnen

### Anwendung
- **Strg+Q**: Anwendung beenden

## Erweiterte Verwendung

### Event-Handling

```csharp
// Auf Änderungen reagieren
ShortcutManager.Instance.ShortcutChanged += (sender, args) =>
{
    switch (args.ChangeType)
    {
        case ShortcutChangeType.Registered:
            Debug.WriteLine($"Shortcut registriert: {args.Shortcut?.DisplayName}");
            break;
        case ShortcutChangeType.Modified:
            Debug.WriteLine($"Shortcut geändert: {args.Shortcut?.DisplayName}");
            break;
        case ShortcutChangeType.Unregistered:
            Debug.WriteLine($"Shortcut entfernt: {args.Shortcut?.DisplayName}");
            break;
        case ShortcutChangeType.Reset:
            Debug.WriteLine("Shortcuts zurückgesetzt");
            break;
    }
};

// Wird ausgelöst, wenn Konfiguration geladen wurde
ShortcutManager.Instance.ShortcutsLoaded += (sender, args) =>
{
    Debug.WriteLine("Shortcuts erfolgreich geladen");
};
```

### Konfigurations-Pfad ändern

```csharp
// Benutzerdefinierten Pfad setzen
ShortcutManager.Instance.SetConfigurationPath(
    @"C:\MyApp\Config\shortcuts.json"
);
```

### Shortcut-Status abfragen

```csharp
// Prüfen, ob Tastenkombination bereits verwendet wird
var gesture = new KeyGesture(Key.S, ModifierKeys.Control);
if (ShortcutManager.Instance.IsGestureInUse(gesture, out var existingShortcut))
{
    MessageBox.Show($"Bereits belegt von: {existingShortcut?.DisplayName}");
}

// Alle Shortcuts einer Kategorie abrufen
var fileShortcuts = ShortcutManager.Instance.GetShortcutsByCategory("Datei");
foreach (var shortcut in fileShortcuts)
{
    Debug.WriteLine($"{shortcut.DisplayName}: {shortcut.KeyGestureText}");
}

// Alle Kategorien abrufen
var categories = ShortcutManager.Instance.GetCategories();
```

## Best Practices

### 1. Eindeutige IDs
Verwenden Sie beschreibende, eindeutige IDs:
```csharp
? "file_save", "edit_copy", "view_zoom_in"
? "s", "c", "1"
```

### 2. Kategorisierung
Gruppieren Sie verwandte Shortcuts:
- **Datei**: Neu, Öffnen, Speichern, Drucken
- **Bearbeiten**: Kopieren, Einfügen, Ausschneiden, Rückgängig
- **Navigation**: Tabs, Panels, Dialoge
- **Ansicht**: Zoom, Vollbild, Panels ein-/ausblenden
- **Tutorial**: Hilfefunktionen
- **System**: Anwendungs-Funktionen

### 3. Standard-Shortcuts verwenden
Halten Sie sich an bekannte Konventionen:
- **Strg+S**: Speichern
- **Strg+O**: Öffnen
- **Strg+N**: Neu
- **Strg+C/X/V**: Kopieren/Ausschneiden/Einfügen
- **Strg+Z/Y**: Rückgängig/Wiederholen
- **Strg+F**: Suchen
- **F1**: Hilfe

### 4. Modifier-Keys nutzen
Vermeiden Sie einzelne Tasten ohne Modifier für globale Shortcuts:
```csharp
? new KeyGesture(Key.S, ModifierKeys.Control)
? new KeyGesture(Key.S) // Konflikt mit Text-Eingabe
```

### 5. System-Shortcuts schützen
```csharp
var criticalShortcut = new ShortcutAction(...);
criticalShortcut.IsModifiable = false; // Benutzer kann nicht ändern
```

### 6. Beschreibungen hinzufügen
Helfen Sie Benutzern zu verstehen, was der Shortcut tut:
```csharp
ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
    "export_data",
    "Daten exportieren",
    new KeyGesture(Key.E, ModifierKeys.Control | ModifierKeys.Shift),
    () => ExportData(),
    "Datei",
    description: "Exportiert alle Daten als CSV-Datei" // ? Hilfreich!
));
```

## Workflow

### Für Entwickler

1. **Shortcuts registrieren** in `InitializeShortcutManager()`
2. **KeyDown-Handler** in Windows/Controls hinzufügen
3. **ShortcutManager.HandleKeyPress()** aufrufen
4. Bei Bedarf: Shortcuts programmatisch ändern

### Für Benutzer

1. **Einstellungen > Tastenkürzel verwalten** öffnen
2. Gewünschten Shortcut finden (ggf. Suchfunktion nutzen)
3. Auf Tastenkürzel klicken
4. Neue Tastenkombination drücken
5. **Speichern** klicken

## Speicherformat

Die Shortcuts werden als JSON gespeichert:

```json
{
  "Version": "1.0",
  "LastModified": "2024-01-15T10:30:00.000Z",
  "Shortcuts": [
    {
      "Id": "save",
      "KeyGestureText": "Ctrl+S",
      "IsEnabled": true,
      "Category": "Datei"
    },
    {
      "Id": "open_settings",
      "KeyGestureText": "Ctrl+OemComma",
      "IsEnabled": true,
      "Category": "Navigation"
    }
  ]
}
```

## Speicherort

Standard: `%APPDATA%\Zenox\shortcuts.json`

Pfad ändern:
```csharp
ShortcutManager.Instance.SetConfigurationPath("C:\\Custom\\Path\\shortcuts.json");
```

## Beispiele

Siehe `Examples\ShortcutRegistrationExamples.cs` für vollständige Beispiele:

- Einfache Shortcuts mit Actions
- Shortcuts mit Commands
- Navigation-Shortcuts (Tab-Switching)
- Mehrfach-Modifier
- Funktionstasten
- Programmatische Verwaltung

## Integration

### In bestehende Manager

```csharp
public class MyCustomManager : BaseManager
{
    public MyCustomManager()
    {
        RegisterShortcuts();
    }
    
    private void RegisterShortcuts()
    {
        ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
            "my_action",
            "Meine Aktion",
            new KeyGesture(Key.M, ModifierKeys.Control | ModifierKeys.Alt),
            command: MyActionCommand,
            category: "Mein Modul"
        ));
    }
}
```

### In ViewModels

```csharp
public class MyViewModel : NotificationObject
{
    public ICommand MyCommand { get; }
    
    public MyViewModel()
    {
        MyCommand = new RelayCommand(_ => ExecuteAction());
        
        // Shortcut für dieses Command registrieren
        ShortcutManager.Instance.RegisterShortcut(new ShortcutAction(
            "viewmodel_action",
            "ViewModel-Aktion",
            new KeyGesture(Key.F7),
            MyCommand,
            category: "View"
        ));
    }
}
```

## Troubleshooting

### Shortcuts funktionieren nicht

1. Prüfen Sie, ob `HandleKeyPress()` im KeyDown-Handler aufgerufen wird
2. Stellen Sie sicher, dass das Control Fokus hat
3. Prüfen Sie, ob der Shortcut aktiviert ist (`IsEnabled = true`)
4. Überprüfen Sie Konflikte mit anderen Shortcuts

### Konflikte

Der Shortcut-Manager zeigt Konflikte automatisch an:
- ?? Warnung in der UI
- `InvalidOperationException` bei programmatischer Änderung

### Konfiguration wird nicht gespeichert

1. Prüfen Sie Schreibrechte für `%APPDATA%\Zenox\`
2. Rufen Sie `SaveConfiguration()` explizit auf
3. Prüfen Sie Logs auf Fehler

## Erweiterungsmöglichkeiten

### Globale Shortcuts (außerhalb der Anwendung)

Für globale Hotkeys benötigen Sie zusätzliche Win32-Integration:
- `RegisterHotKey()` / `UnregisterHotKey()` API
- Window Message Handler für `WM_HOTKEY`

### Maus-Gesten

Das aktuelle System unterstützt nur Tastatur-Shortcuts. Für Maus-Gesten:
- Erweitern Sie `ShortcutAction` um `MouseGesture`
- Implementieren Sie Mouse-Event-Handling

### Makros

Für komplexere Aktionsfolgen:
- Erstellen Sie `MacroAction` als Erweiterung
- Speichern Sie Sequenzen von Commands
- Implementieren Sie Recorder-Funktion

## API-Referenz

### ShortcutManager Methods

- `RegisterShortcut(ShortcutAction)` - Registriert einen neuen Shortcut
- `UnregisterShortcut(string id)` - Entfernt einen Shortcut
- `GetShortcut(string id)` - Ruft einen Shortcut ab
- `GetShortcutsByCategory(string)` - Alle Shortcuts einer Kategorie
- `GetCategories()` - Alle verfügbaren Kategorien
- `UpdateShortcutKey(string id, KeyGesture)` - Ändert Tastenkombination
- `IsGestureInUse(KeyGesture, out ShortcutAction)` - Konflikt-Prüfung
- `HandleKeyPress(Key, ModifierKeys)` - Verarbeitet Tastatur-Event
- `SaveConfiguration()` - Speichert Konfiguration
- `LoadConfiguration()` - Lädt Konfiguration
- `ResetToDefault(string id)` - Setzt einen Shortcut zurück
- `ResetAllToDefaults()` - Setzt alle Shortcuts zurück
- `Clear()` - Entfernt alle Shortcuts

### Events

- `ShortcutChanged` - Wird bei Änderungen ausgelöst
- `ShortcutsLoaded` - Wird nach dem Laden ausgelöst

## Lizenz

Teil des Zenox.NET Frameworks

