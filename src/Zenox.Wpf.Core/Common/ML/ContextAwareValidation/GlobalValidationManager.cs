using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zenox.Wpf.Core.Common.MVVM.FactoryInjection;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Globaler Manager für automatische kontextbewusste Validierung.
    /// Registriert sich automatisch bei allen relevanten UI-Elementen ohne manuelle Konfiguration.
    /// </summary>
    public class GlobalValidationManager : AppObject
    {
        private static GlobalValidationManager? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// Singleton-Instanz des Managers.
        /// </summary>
        public static GlobalValidationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new GlobalValidationManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gibt an, ob der Manager aktiv ist.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Liste von Element-Typen, die automatisch überwacht werden.
        /// </summary>
        public HashSet<Type> MonitoredTypes { get; } = new()
        {
            typeof(TextBox),
            typeof(TextBlock),
            typeof(Label),
            typeof(ComboBox),
            typeof(DatePicker)
        };

        /// <summary>
        /// Property-Namen, die automatisch validiert werden sollen.
        /// </summary>
        public HashSet<string> AutoValidateProperties { get; } = new()
        {
            "Text", "Content", "SelectedValue", "Value", "Number"
        };

        /// <summary>
        /// Ausgeschlossene Element-Namen (z.B. interne Controls).
        /// </summary>
        public HashSet<string> ExcludedNames { get; } = new()
        {
            "PART_", "Adorner", "Chrome", "Thumb"
        };

        /// <summary>
        /// Registrierte Fenster.
        /// </summary>
        private readonly HashSet<Window> _registeredWindows = new();

        /// <summary>
        /// Key-Kombination für Validierung (Optional, für direkten Trigger).
        /// </summary>
        public ModifierKeys ValidationModifier { get; set; } = ModifierKeys.Shift;

        /// <summary>
        /// Gibt an, ob Kontextmenü verwendet werden soll (Standard: true).
        /// </summary>
        public bool UseContextMenu { get; set; } = true;

        /// <summary>
        /// Text für den Kontextmenü-Eintrag.
        /// </summary>
        public string ContextMenuText { get; set; } = "?? KI Validierung";

        /// <summary>
        /// Event das gefeuert wird, wenn ein Element validiert wurde.
        /// </summary>
        public event EventHandler<ElementValidatedEventArgs>? ElementValidated;

        private GlobalValidationManager()
        {
            // Automatisch bei neuen Fenstern registrieren
            if (Application.Current != null)
            {
                Application.Current.Activated += (s, e) =>
                {
                    if (s is Window window && !_registeredWindows.Contains(window))
                    {
                        RegisterWindow(window);
                    }
                };
            }
        }

        /// <summary>
        /// Initialisiert den globalen Manager und registriert alle offenen Fenster.
        /// </summary>
        public void Initialize()
        {
            if (Application.Current?.Windows == null)
                return;

            foreach (Window window in Application.Current.Windows)
            {
                RegisterWindow(window);
            }

            Logger?.Eintragen("GlobalValidationManager: Initialisiert");
        }

        /// <summary>
        /// Registriert ein Fenster für automatische Validierung.
        /// </summary>
        public void RegisterWindow(Window window)
        {
            if (_registeredWindows.Contains(window) || !IsEnabled)
                return;

            _registeredWindows.Add(window);

            // Event-Handler für das Laden von Content
            window.Loaded += Window_Loaded;
            window.Closed += Window_Closed;

            // Bereits geladenen Content verarbeiten
            if (window.IsLoaded)
            {
                AttachToVisualTree(window);
            }

            Logger?.Eintragen($"GlobalValidationManager: Fenster '{window.GetType().Name}' registriert");
        }

        /// <summary>
        /// Entfernt die Registrierung eines Fensters.
        /// </summary>
        public void UnregisterWindow(Window window)
        {
            if (!_registeredWindows.Contains(window))
                return;

            _registeredWindows.Remove(window);
            window.Loaded -= Window_Loaded;
            window.Closed -= Window_Closed;

            DetachFromVisualTree(window);

            Logger?.Eintragen($"GlobalValidationManager: Fenster '{window.GetType().Name}' deregistriert");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                AttachToVisualTree(window);
            }
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                UnregisterWindow(window);
            }
        }

        /// <summary>
        /// Durchläuft den Visual Tree und registriert alle relevanten Elemente.
        /// </summary>
        private void AttachToVisualTree(DependencyObject root)
        {
            if (root == null)
                return;

            // Aktuelles Element prüfen
            if (root is FrameworkElement element && ShouldMonitorElement(element))
            {
                AttachValidationToElement(element);
            }

            // Rekursiv durch Children
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                AttachToVisualTree(child);
            }

            // Auch LogicalChildren prüfen (für Templates etc.)
            if (root is FrameworkElement fe && fe.IsLoaded)
            {
                foreach (var logicalChild in LogicalTreeHelper.GetChildren(fe))
                {
                    if (logicalChild is DependencyObject depObj)
                    {
                        AttachToVisualTree(depObj);
                    }
                }
            }
        }

        /// <summary>
        /// Entfernt Validierung vom Visual Tree.
        /// </summary>
        private void DetachFromVisualTree(DependencyObject root)
        {
            if (root == null)
                return;

            if (root is FrameworkElement element)
            {
                DetachValidationFromElement(element);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                DetachFromVisualTree(child);
            }
        }

        /// <summary>
        /// Prüft, ob ein Element überwacht werden soll.
        /// </summary>
        private bool ShouldMonitorElement(FrameworkElement element)
        {
            // Typ prüfen
            if (!MonitoredTypes.Any(t => t.IsInstanceOfType(element)))
                return false;

            // Name prüfen (ausschließen)
            if (!string.IsNullOrEmpty(element.Name) && 
                ExcludedNames.Any(ex => element.Name.StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Muss DataContext oder Binding haben
            if (element.DataContext == null)
                return false;

            // Muss validierbare Property haben
            return HasValidatableProperty(element);
        }

        /// <summary>
        /// Prüft, ob das Element eine validierbare Property hat.
        /// </summary>
        private bool HasValidatableProperty(FrameworkElement element)
        {
            var type = element.GetType();
            foreach (var propName in AutoValidateProperties)
            {
                var prop = type.GetProperty(propName);
                if (prop != null && prop.CanRead)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Registriert Validierung für ein Element.
        /// </summary>
        private void AttachValidationToElement(FrameworkElement element)
        {
            // Prüfen ob bereits registriert
            if (element.Tag is string tag && tag == "ValidationAttached")
                return;

            if (UseContextMenu)
            {
                // Kontextmenü-Ansatz
                element.ContextMenuOpening += Element_ContextMenuOpening;
            }
            else
            {
                // Direkter Mouse-Handler für Rechtsklick mit Modifier
                element.MouseRightButtonDown += Element_MouseRightButtonDown;
            }

            element.Tag = "ValidationAttached";
        }

        /// <summary>
        /// Entfernt Validierung von einem Element.
        /// </summary>
        private void DetachValidationFromElement(FrameworkElement element)
        {
            element.ContextMenuOpening -= Element_ContextMenuOpening;
            element.MouseRightButtonDown -= Element_MouseRightButtonDown;
            
            if (element.Tag is string tag && tag == "ValidationAttached")
            {
                element.Tag = null;
            }
        }

        /// <summary>
        /// Handler für das Öffnen des Kontextmenüs.
        /// </summary>
        private void Element_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!IsEnabled || sender is not FrameworkElement element)
                return;

            // Erstelle oder aktualisiere das Kontextmenü
            var contextMenu = element.ContextMenu;
            if (contextMenu == null)
            {
                contextMenu = new ContextMenu();
                element.ContextMenu = contextMenu;
            }

            // Prüfe ob bereits ein KI-Validierungs-Menüeintrag existiert
            var existingItem = contextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(m => m.Tag?.ToString() == "AIValidation");

            if (existingItem == null)
            {
                // Separator hinzufügen, falls bereits andere Items existieren
                if (contextMenu.Items.Count > 0)
                {
                    var separator = new Separator { Tag = "AIValidationSeparator" };
                    contextMenu.Items.Add(separator);
                }

                // Erstelle KI-Validierungs-MenuItem
                var menuItem = new MenuItem
                {
                    Header = ContextMenuText,
                    Tag = "AIValidation",
                    FontWeight = FontWeights.SemiBold
                };

                // Icon/Symbol hinzufügen (optional)
                var icon = new TextBlock
                {
                    Text = "??",
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                menuItem.Icon = icon;

                // Click-Handler für Validierung
                menuItem.Click += async (s, args) =>
                {
                    await PerformValidationAsync(element);
                };

                contextMenu.Items.Add(menuItem);
            }
        }

        /// <summary>
        /// Handler für Rechtsklick auf einem Element (Fallback ohne Kontextmenü).
        /// </summary>
        private async void Element_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled || sender is not FrameworkElement element)
                return;

            // Prüfe Modifier-Key
            if (Keyboard.Modifiers != ValidationModifier)
                return;

            // Event markieren als behandelt, damit kein Context-Menu erscheint
            e.Handled = true;

            await PerformValidationAsync(element);
        }

        /// <summary>
        /// Führt die KI-Validierung für ein Element durch.
        /// </summary>
        private async Task PerformValidationAsync(FrameworkElement element)
        {
            try
            {
                // Kontext automatisch extrahieren
                var extractor = new AutomaticContextExtractor();
                var context = extractor.ExtractContext(element);

                if (context == null || (context.Value == null && string.IsNullOrEmpty(context.ValueAsString)))
                {
                    Logger?.Eintragen("GlobalValidationManager: Kein validierbarer Wert gefunden");
                    ShowErrorFeedback(element, "Kein validierbarer Wert gefunden");
                    return;
                }

                // Validierung durchführen
                var elementId = GetElementId(element);
                var (result, stateMachine) = await ContextAwareValidationService.Instance.ValidateAsync(context, elementId);

                // Feedback anzeigen
                var feedbackManager = new ValidationFeedbackManager();
                feedbackManager.ShowFeedback(element, result, stateMachine);

                // Event auslösen
                ElementValidated?.Invoke(this, new ElementValidatedEventArgs(element, context, result));

                Logger?.Eintragen($"GlobalValidationManager: Element validiert - {context.FieldName} = {context.ValueAsString} -> {result.State}");
            }
            catch (Exception ex)
            {
                Logger?.LogException(ex, "GlobalValidationManager.PerformValidationAsync");
                ShowErrorFeedback(element, "Fehler bei der Validierung");
            }
        }

        /// <summary>
        /// Zeigt ein Fehler-Feedback an.
        /// </summary>
        private void ShowErrorFeedback(FrameworkElement element, string message)
        {
            var result = new ValidationResult
            {
                State = ValidationState.Error,
                ErrorMessage = message,
                Summary = message,
                Confidence = 1.0
            };

            var feedbackManager = new ValidationFeedbackManager();
            feedbackManager.ShowFeedback(element, result, null);
        }

        /// <summary>
        /// Generiert eine eindeutige ID für ein Element.
        /// </summary>
        private string GetElementId(FrameworkElement element)
        {
            if (!string.IsNullOrEmpty(element.Name))
                return element.Name;

            return $"{element.GetType().Name}_{element.GetHashCode()}";
        }

        /// <summary>
        /// Fügt einen benutzerdefinierten Element-Typ zur Überwachung hinzu.
        /// </summary>
        public void AddMonitoredType<T>() where T : FrameworkElement
        {
            MonitoredTypes.Add(typeof(T));
        }

        /// <summary>
        /// Entfernt einen Element-Typ von der Überwachung.
        /// </summary>
        public void RemoveMonitoredType<T>() where T : FrameworkElement
        {
            MonitoredTypes.Remove(typeof(T));
        }
    }

    /// <summary>
    /// EventArgs für Element-Validierung.
    /// </summary>
    public class ElementValidatedEventArgs : EventArgs
    {
        public FrameworkElement Element { get; }
        public ValidationContext Context { get; }
        public ValidationResult Result { get; }

        public ElementValidatedEventArgs(FrameworkElement element, ValidationContext context, ValidationResult result)
        {
            Element = element;
            Context = context;
            Result = result;
        }
    }
}
