using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Zenox.Wpf.Core.Common.Notifications;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Attached Behavior für kontextbewusste ML-Validierung per Rechtsklick.
    /// Ermöglicht die Validierung von Werten in beliebigen UI-Elementen.
    /// </summary>
    public static class ContextAwareValidationBehavior
    {
        #region Attached Properties

        /// <summary>
        /// Aktiviert die kontextbewusste Validierung für ein Element.
        /// </summary>
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        /// <summary>
        /// Der Name des Feldes für die Validierung.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.RegisterAttached(
                "FieldName",
                typeof(string),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Die Kategorie für die Validierung.
        /// </summary>
        public static readonly DependencyProperty CategoryProperty =
            DependencyProperty.RegisterAttached(
                "Category",
                typeof(string),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Der Name der View für Kontext-Erfassung.
        /// </summary>
        public static readonly DependencyProperty ViewNameProperty =
            DependencyProperty.RegisterAttached(
                "ViewName",
                typeof(string),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Erwarteter Minimalwert.
        /// </summary>
        public static readonly DependencyProperty ExpectedMinProperty =
            DependencyProperty.RegisterAttached(
                "ExpectedMin",
                typeof(double?),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Erwarteter Maximalwert.
        /// </summary>
        public static readonly DependencyProperty ExpectedMaxProperty =
            DependencyProperty.RegisterAttached(
                "ExpectedMax",
                typeof(double?),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Feedback-Konfiguration.
        /// </summary>
        public static readonly DependencyProperty FeedbackConfigProperty =
            DependencyProperty.RegisterAttached(
                "FeedbackConfig",
                typeof(ValidationFeedbackConfiguration),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Speichert den ursprünglichen Border-Brush für Reset.
        /// </summary>
        private static readonly DependencyProperty OriginalBorderBrushProperty =
            DependencyProperty.RegisterAttached(
                "OriginalBorderBrush",
                typeof(Brush),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Speichert die aktive StateMachine.
        /// </summary>
        private static readonly DependencyProperty StateMachineProperty =
            DependencyProperty.RegisterAttached(
                "StateMachine",
                typeof(ValidationStateMachine),
                typeof(ContextAwareValidationBehavior),
                new PropertyMetadata(null));

        #endregion

        #region Getters/Setters

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static string? GetFieldName(DependencyObject obj) => (string?)obj.GetValue(FieldNameProperty);
        public static void SetFieldName(DependencyObject obj, string? value) => obj.SetValue(FieldNameProperty, value);

        public static string? GetCategory(DependencyObject obj) => (string?)obj.GetValue(CategoryProperty);
        public static void SetCategory(DependencyObject obj, string? value) => obj.SetValue(CategoryProperty, value);

        public static string? GetViewName(DependencyObject obj) => (string?)obj.GetValue(ViewNameProperty);
        public static void SetViewName(DependencyObject obj, string? value) => obj.SetValue(ViewNameProperty, value);

        public static double? GetExpectedMin(DependencyObject obj) => (double?)obj.GetValue(ExpectedMinProperty);
        public static void SetExpectedMin(DependencyObject obj, double? value) => obj.SetValue(ExpectedMinProperty, value);

        public static double? GetExpectedMax(DependencyObject obj) => (double?)obj.GetValue(ExpectedMaxProperty);
        public static void SetExpectedMax(DependencyObject obj, double? value) => obj.SetValue(ExpectedMaxProperty, value);

        public static ValidationFeedbackConfiguration? GetFeedbackConfig(DependencyObject obj) => 
            (ValidationFeedbackConfiguration?)obj.GetValue(FeedbackConfigProperty);
        public static void SetFeedbackConfig(DependencyObject obj, ValidationFeedbackConfiguration? value) => 
            obj.SetValue(FeedbackConfigProperty, value);

        #endregion

        #region Event Handlers

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
                return;

            if ((bool)e.NewValue)
            {
                // ContextMenu erstellen/erweitern
                EnsureContextMenu(element);
                element.Loaded += Element_Loaded;
            }
            else
            {
                element.Loaded -= Element_Loaded;
                RemoveContextMenuItem(element);
            }
        }

        private static void Element_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                // Original-Border speichern
                SaveOriginalBorder(element);
            }
        }

        private static void EnsureContextMenu(FrameworkElement element)
        {
            element.ContextMenu ??= new ContextMenu();

            // Prüfen ob MenuItem bereits existiert
            foreach (var item in element.ContextMenu.Items)
            {
                if (item is MenuItem mi && mi.Tag as string == "AIValidation")
                    return;
            }

            // Separator hinzufügen wenn andere Items vorhanden
            if (element.ContextMenu.Items.Count > 0)
            {
                element.ContextMenu.Items.Add(new Separator());
            }

            // MenuItem erstellen
            var validateMenuItem = new MenuItem
            {
                Header = "?? Mit AI validieren",
                Tag = "AIValidation",
                Icon = new TextBlock { Text = "?", FontWeight = FontWeights.Bold }
            };
            validateMenuItem.Click += (s, e) => ValidateElement(element);

            element.ContextMenu.Items.Add(validateMenuItem);

            // Untermenü für erweiterte Optionen
            var advancedMenuItem = new MenuItem
            {
                Header = "AI-Validierung Optionen",
                Tag = "AIValidationOptions"
            };

            var showHistoryItem = new MenuItem { Header = "?? Historische Werte anzeigen" };
            showHistoryItem.Click += (s, e) => ShowHistory(element);

            var resetItem = new MenuItem { Header = "?? Validierung zurücksetzen" };
            resetItem.Click += (s, e) => ResetValidation(element);

            advancedMenuItem.Items.Add(showHistoryItem);
            advancedMenuItem.Items.Add(resetItem);

            element.ContextMenu.Items.Add(advancedMenuItem);
        }

        private static void RemoveContextMenuItem(FrameworkElement element)
        {
            if (element.ContextMenu == null)
                return;

            var itemsToRemove = new List<object>();
            foreach (var item in element.ContextMenu.Items)
            {
                if (item is MenuItem mi && (mi.Tag as string == "AIValidation" || mi.Tag as string == "AIValidationOptions"))
                    itemsToRemove.Add(item);
                else if (item is Separator && itemsToRemove.Count > 0)
                    itemsToRemove.Add(item);
            }

            foreach (var item in itemsToRemove)
            {
                element.ContextMenu.Items.Remove(item);
            }
        }

        #endregion

        #region Validation Logic

        private static async void ValidateElement(FrameworkElement element)
        {
            try
            {
                // Kontext aus Element sammeln
                var context = BuildValidationContext(element);

                if (context.Value == null && string.IsNullOrEmpty(context.ValueAsString))
                {
                    NotificationService.Instance?.ShowWarning(
                        "Keine Daten",
                        "Kein Wert zum Validieren gefunden.");
                    return;
                }

                // Validierung durchführen
                var elementId = GetElementId(element);
                var (result, stateMachine) = await ContextAwareValidationService.Instance.ValidateAsync(context, elementId);

                // StateMachine speichern
                element.SetValue(StateMachineProperty, stateMachine);

                // Feedback-Handler registrieren
                var config = GetFeedbackConfig(element) ?? new ValidationFeedbackConfiguration();
                stateMachine.FeedbackConfig = config;
                stateMachine.FeedbackRequested += (s, e) => HandleFeedback(element, e);

                // Visuelles Feedback anwenden
                ApplyVisualFeedback(element, result);
            }
            catch (Exception ex)
            {
                NotificationService.Instance?.ShowError(
                    "Validierungsfehler",
                    ex.Message);
            }
        }

        private static ValidationContext BuildValidationContext(FrameworkElement element)
        {
            var context = new ValidationContext
            {
                FieldName = GetFieldName(element),
                Category = GetCategory(element),
                ViewName = GetViewName(element) ?? GetParentViewName(element),
                ExpectedMin = GetExpectedMin(element),
                ExpectedMax = GetExpectedMax(element)
            };

            // Wert aus Element extrahieren
            ExtractValue(element, context);

            // ViewModel-Informationen extrahieren
            ExtractViewModelInfo(element, context);

            // Verwandte Werte aus dem DataContext sammeln
            ExtractRelatedValues(element, context);

            return context;
        }

        private static void ExtractValue(FrameworkElement element, ValidationContext context)
        {
            // Versuche Wert aus verschiedenen Element-Typen zu extrahieren
            object? value = element switch
            {
                TextBox tb => tb.Text,
                TextBlock tb => tb.Text,
                Label lbl => lbl.Content,
                ContentControl cc => cc.Content,
                _ => null
            };

            // Versuche über Binding zu extrahieren
            if (value == null)
            {
                var binding = GetBinding(element);
                if (binding != null && element.DataContext != null)
                {
                    value = GetPropertyValue(element.DataContext, binding.Path.Path);
                    context.BindingPath = binding.Path.Path;

                    // Feldname aus Binding-Path ableiten falls nicht gesetzt
                    if (string.IsNullOrEmpty(context.FieldName))
                    {
                        context.FieldName = binding.Path.Path.Split('.')[^1];
                    }
                }
            }

            context.Value = value;
            context.ValueAsString = value?.ToString();
            context.ValueType = value?.GetType().Name;
        }

        private static Binding? GetBinding(FrameworkElement element)
        {
            // Versuche Binding für verschiedene Properties zu finden
            var textProperty = element switch
            {
                TextBox => TextBox.TextProperty,
                TextBlock => TextBlock.TextProperty,
                Label => ContentControl.ContentProperty,
                ContentControl => ContentControl.ContentProperty,
                _ => null
            };

            if (textProperty != null)
            {
                var bindingExpression = element.GetBindingExpression(textProperty);
                return bindingExpression?.ParentBinding;
            }

            return null;
        }

        private static void ExtractViewModelInfo(FrameworkElement element, ValidationContext context)
        {
            var dataContext = element.DataContext;
            if (dataContext != null)
            {
                context.ViewModelName = dataContext.GetType().Name;

                // Entity-Name aus Property extrahieren
                var entityProp = dataContext.GetType().GetProperty("Entity") ?? 
                                 dataContext.GetType().GetProperty("Model") ??
                                 dataContext.GetType().GetProperty("Item");
                if (entityProp != null)
                {
                    var entity = entityProp.GetValue(dataContext);
                    if (entity != null)
                    {
                        context.EntityName = entity.GetType().Name;
                        
                        // ID extrahieren
                        var idProp = entity.GetType().GetProperty("Id") ?? 
                                     entity.GetType().GetProperty("ID");
                        if (idProp != null)
                        {
                            context.EntityId = idProp.GetValue(entity)?.ToString();
                        }
                    }
                }
            }
        }

        private static void ExtractRelatedValues(FrameworkElement element, ValidationContext context)
        {
            var dataContext = element.DataContext;
            if (dataContext == null)
                return;

            // Numerische Properties als verwandte Werte sammeln
            foreach (var prop in dataContext.GetType().GetProperties())
            {
                if (prop.Name == context.FieldName)
                    continue;

                try
                {
                    var value = prop.GetValue(dataContext);
                    if (value is int or long or float or double or decimal)
                    {
                        context.RelatedValues[prop.Name] = value;
                    }
                }
                catch
                {
                    // Property-Zugriff fehlgeschlagen - ignorieren
                }
            }
        }

        private static string? GetParentViewName(FrameworkElement element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is Window window)
                    return window.GetType().Name;
                if (parent is UserControl uc)
                    return uc.GetType().Name;
                if (parent is Page page)
                    return page.GetType().Name;
                
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static object? GetPropertyValue(object obj, string path)
        {
            var parts = path.Split('.');
            object? current = obj;

            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                var prop = current.GetType().GetProperty(part);
                if (prop == null)
                    return null;

                current = prop.GetValue(current);
            }

            return current;
        }

        private static string GetElementId(FrameworkElement element)
        {
            if (!string.IsNullOrEmpty(element.Name))
                return element.Name;

            return element.GetHashCode().ToString();
        }

        #endregion

        #region Visual Feedback

        private static void HandleFeedback(FrameworkElement element, ValidationFeedbackEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (e.FeedbackType)
                {
                    case ValidationFeedbackType.Toast:
                        ShowToast(e.Message, e.Severity);
                        break;
                    case ValidationFeedbackType.BorderHighlight:
                        ApplyBorderHighlight(element, e.Severity);
                        break;
                    case ValidationFeedbackType.BackgroundHighlight:
                        ApplyBackgroundHighlight(element, e.Severity);
                        break;
                    case ValidationFeedbackType.Pulse:
                        ApplyPulseAnimation(element);
                        break;
                    case ValidationFeedbackType.Tooltip:
                        ShowTooltip(element, e.Message);
                        break;
                    case ValidationFeedbackType.None:
                        ResetVisualFeedback(element);
                        break;
                    case ValidationFeedbackType.All:
                        ApplyBorderHighlight(element, e.Severity);
                        ShowToast(e.Message, e.Severity);
                        break;
                }
            });
        }

        private static void ApplyVisualFeedback(FrameworkElement element, ValidationResult result)
        {
            var color = result.State switch
            {
                ValidationState.Valid => Colors.Green,
                ValidationState.Warning => Colors.Orange,
                ValidationState.Anomaly => Colors.Red,
                ValidationState.Error => Colors.DarkRed,
                _ => Colors.Gray
            };

            ApplyBorderHighlight(element, result.State switch
            {
                ValidationState.Valid => "success",
                ValidationState.Warning => "warning",
                ValidationState.Anomaly => "error",
                _ => "info"
            });

            // Toast anzeigen wenn es Probleme gibt
            if (result.HasIssue)
            {
                var severity = result.State == ValidationState.Warning ? "warning" : "error";
                var title = result.State == ValidationState.Warning ? "Warnung" : "Anomalie erkannt";
                var message = result.Summary;

                if (result.ExpectedValue.HasValue)
                {
                    message += $"\nErwartet: ~{result.ExpectedValue:F2}";
                }
                if (result.DeviationPercentage.HasValue)
                {
                    message += $"\nAbweichung: {result.DeviationPercentage:F1}%";
                }

                ShowToast(message ?? "Validierungsproblem erkannt", severity);
            }
            else if (result.State == ValidationState.Valid)
            {
                NotificationService.Instance?.ShowSuccess(
                    "Validierung OK",
                    result.Summary ?? "Der Wert ist plausibel.");
            }
        }

        private static void ShowToast(string message, string severity)
        {
            switch (severity)
            {
                case "success":
                    NotificationService.Instance?.ShowSuccess("Validierung", message);
                    break;
                case "warning":
                    NotificationService.Instance?.ShowWarning("Validierung", message);
                    break;
                case "error":
                    NotificationService.Instance?.ShowError("Validierung", message);
                    break;
                default:
                    NotificationService.Instance?.ShowInfo("Validierung", message);
                    break;
            }
        }

        private static void ApplyBorderHighlight(FrameworkElement element, string severity)
        {
            var brush = severity switch
            {
                "success" => new SolidColorBrush(Colors.Green),
                "warning" => new SolidColorBrush(Colors.Orange),
                "error" => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Blue)
            };

            // Für Control-basierte Elemente
            if (element is Control control)
            {
                control.BorderBrush = brush;
                control.BorderThickness = new Thickness(2);
            }
            else if (element is Border border)
            {
                border.BorderBrush = brush;
                border.BorderThickness = new Thickness(2);
            }

            // Animation für Fade-out nach einiger Zeit
            var animation = new ColorAnimation
            {
                To = Colors.Transparent,
                Duration = TimeSpan.FromSeconds(3),
                BeginTime = TimeSpan.FromSeconds(2)
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private static void ApplyBackgroundHighlight(FrameworkElement element, string severity)
        {
            var color = severity switch
            {
                "success" => Color.FromArgb(50, 0, 255, 0),
                "warning" => Color.FromArgb(50, 255, 165, 0),
                "error" => Color.FromArgb(50, 255, 0, 0),
                _ => Color.FromArgb(50, 0, 0, 255)
            };

            var brush = new SolidColorBrush(color);

            if (element is Control control)
            {
                control.Background = brush;
            }
            else if (element is Panel panel)
            {
                panel.Background = brush;
            }
        }

        private static void ApplyPulseAnimation(FrameworkElement element)
        {
            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.7,
                Duration = TimeSpan.FromMilliseconds(300),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };

            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private static void ShowTooltip(FrameworkElement element, string message)
        {
            element.ToolTip = new ToolTip
            {
                Content = message,
                IsOpen = true,
                StaysOpen = false
            };
        }

        private static void SaveOriginalBorder(FrameworkElement element)
        {
            if (element is Control control && element.GetValue(OriginalBorderBrushProperty) == null)
            {
                element.SetValue(OriginalBorderBrushProperty, control.BorderBrush);
            }
        }

        private static void ResetVisualFeedback(FrameworkElement element)
        {
            if (element is Control control)
            {
                var original = element.GetValue(OriginalBorderBrushProperty) as Brush;
                if (original != null)
                {
                    control.BorderBrush = original;
                }
                control.BorderThickness = new Thickness(1);
            }
        }

        private static void ResetValidation(FrameworkElement element)
        {
            var stateMachine = element.GetValue(StateMachineProperty) as ValidationStateMachine;
            stateMachine?.Reset();
            ResetVisualFeedback(element);
            
            NotificationService.Instance?.ShowInfo(
                "Zurückgesetzt",
                "Validierung wurde zurückgesetzt.");
        }

        private static void ShowHistory(FrameworkElement element)
        {
            // Placeholder für Historie-Anzeige
            NotificationService.Instance?.ShowInfo(
                "Historie",
                "Feature in Entwicklung: Historische Werte anzeigen");
        }

        #endregion
    }
}
