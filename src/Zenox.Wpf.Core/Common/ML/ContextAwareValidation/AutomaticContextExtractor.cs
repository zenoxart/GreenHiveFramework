using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Zenox.Wpf.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Extrahiert automatisch den Validierungskontext aus UI-Elementen.
    /// Intelligente Erkennung von Werten, Bindings und verwandten Daten.
    /// </summary>
    public class AutomaticContextExtractor
    {
        /// <summary>
        /// Extrahiert den vollständigen Validierungskontext aus einem UI-Element.
        /// </summary>
        public ValidationContext? ExtractContext(FrameworkElement element)
        {
            var context = new ValidationContext();

            // 1. Wert extrahieren
            if (!ExtractValue(element, context))
                return null;

            // 2. Feldname aus verschiedenen Quellen ableiten
            context.FieldName = DeriveFieldName(element, context);

            // 3. View-Informationen sammeln
            ExtractViewInfo(element, context);

            // 4. ViewModel und Entity-Informationen
            ExtractDataContextInfo(element, context);

            // 5. Verwandte Werte aus dem DataContext
            ExtractRelatedValues(element, context);

            // 6. Kategorie aus Namespace/Assembly ableiten
            context.Category = DeriveCategory(element);

            // 7. Erwartete Grenzen aus Validation-Rules ableiten
            ExtractValidationRules(element, context);

            return context;
        }

        /// <summary>
        /// Extrahiert den Wert aus verschiedenen Element-Typen.
        /// </summary>
        private bool ExtractValue(FrameworkElement element, ValidationContext context)
        {
            object? value = null;
            DependencyProperty? boundProperty = null;

            // Typ-spezifische Wert-Extraktion
            switch (element)
            {
                case TextBox textBox:
                    value = textBox.Text;
                    boundProperty = TextBox.TextProperty;
                    break;

                case TextBlock textBlock:
                    value = textBlock.Text;
                    boundProperty = TextBlock.TextProperty;
                    break;

                case Label label:
                    value = label.Content;
                    boundProperty = ContentControl.ContentProperty;
                    break;

                case ComboBox comboBox:
                    value = comboBox.SelectedValue ?? comboBox.SelectedItem;
                    boundProperty = ComboBox.SelectedValueProperty;
                    break;

                case DatePicker datePicker:
                    value = datePicker.SelectedDate;
                    boundProperty = DatePicker.SelectedDateProperty;
                    break;

                case ContentControl contentControl:
                    value = contentControl.Content;
                    boundProperty = ContentControl.ContentProperty;
                    break;

                default:
                    // Generischer Ansatz: Suche nach bekannten Properties
                    value = TryGetPropertyValue(element, "Text") ??
                            TryGetPropertyValue(element, "Content") ??
                            TryGetPropertyValue(element, "Value") ??
                            TryGetPropertyValue(element, "SelectedValue");
                    break;
            }

            // Binding-Informationen extrahieren
            if (boundProperty != null)
            {
                var binding = BindingOperations.GetBinding(element, boundProperty);
                if (binding != null)
                {
                    context.BindingPath = binding.Path?.Path;
                    
                    // Wert über Binding-Path aus DataContext holen (falls nicht direkt verfügbar)
                    if (value == null && element.DataContext != null && !string.IsNullOrEmpty(binding.Path?.Path))
                    {
                        value = GetPropertyValue(element.DataContext, binding.Path.Path);
                    }
                }
            }

            context.Value = value;
            context.ValueAsString = value?.ToString();
            context.ValueType = value?.GetType().Name;

            return value != null || !string.IsNullOrEmpty(context.ValueAsString);
        }

        /// <summary>
        /// Leitet den Feldnamen aus verschiedenen Quellen ab.
        /// </summary>
        private string? DeriveFieldName(FrameworkElement element, ValidationContext context)
        {
            // 1. Aus Binding-Path
            if (!string.IsNullOrEmpty(context.BindingPath))
            {
                var parts = context.BindingPath.Split('.');
                return parts[^1]; // Letzter Teil des Paths
            }

            // 2. Aus Element-Name
            if (!string.IsNullOrEmpty(element.Name))
            {
                // Entferne Suffixe wie "TextBox", "Label" etc.
                var name = element.Name;
                var suffixes = new[] { "TextBox", "Label", "ComboBox", "DatePicker", "Text", "Lbl", "Txt", "Cmb" };
                foreach (var suffix in suffixes)
                {
                    if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name[..^suffix.Length];
                        break;
                    }
                }
                return name;
            }

            // 3. Aus Label in der Nähe (Grid, StackPanel)
            var nearbyLabel = FindNearbyLabel(element);
            if (nearbyLabel != null)
            {
                var labelText = nearbyLabel.Content?.ToString();
                if (!string.IsNullOrEmpty(labelText))
                {
                    // Entferne Sonderzeichen wie ":", "*" etc.
                    return labelText.TrimEnd(':', '*', ' ');
                }
            }

            // 4. Aus ToolTip
            if (element.ToolTip is string tooltip && !string.IsNullOrEmpty(tooltip))
            {
                return tooltip.Split(':')[0].Trim();
            }

            // 5. Fallback: Typ-Name
            return element.GetType().Name;
        }

        /// <summary>
        /// Sucht nach einem Label in der Nähe des Elements.
        /// </summary>
        private Label? FindNearbyLabel(FrameworkElement element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            if (parent == null)
                return null;

            // In Grid: Suche in derselben Zeile, vorheriger Spalte
            if (parent is Grid grid)
            {
                var row = Grid.GetRow(element);
                var col = Grid.GetColumn(element);

                foreach (var child in grid.Children.OfType<Label>())
                {
                    if (Grid.GetRow(child) == row && Grid.GetColumn(child) == col - 1)
                        return child;
                }
            }

            // In StackPanel: Vorheriges Element
            if (parent is StackPanel stackPanel)
            {
                var children = stackPanel.Children.Cast<UIElement>().ToList();
                var index = children.IndexOf(element);
                if (index > 0 && children[index - 1] is Label label)
                    return label;
            }

            return null;
        }

        /// <summary>
        /// Extrahiert View-Informationen.
        /// </summary>
        private void ExtractViewInfo(FrameworkElement element, ValidationContext context)
        {
            // Durchlaufe Visual Tree nach oben
            DependencyObject? current = element;
            while (current != null)
            {
                if (current is Window window)
                {
                    context.ViewName = window.GetType().Name;
                    break;
                }
                if (current is UserControl userControl)
                {
                    context.ViewName = userControl.GetType().Name;
                    break;
                }
                if (current is Page page)
                {
                    context.ViewName = page.GetType().Name;
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        /// <summary>
        /// Extrahiert DataContext/ViewModel-Informationen.
        /// </summary>
        private void ExtractDataContextInfo(FrameworkElement element, ValidationContext context)
        {
            var dataContext = element.DataContext;
            if (dataContext == null)
                return;

            var type = dataContext.GetType();
            context.ViewModelName = type.Name;

            // Versuche Entity zu finden
            var entityProperties = new[] { "Entity", "Model", "Item", "CurrentItem", "SelectedItem" };
            foreach (var propName in entityProperties)
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    try
                    {
                        var entity = prop.GetValue(dataContext);
                        if (entity != null && !IsPrimitiveType(entity.GetType()))
                        {
                            context.EntityName = entity.GetType().Name;
                            
                            // ID extrahieren
                            var idProp = entity.GetType().GetProperty("Id") ?? 
                                         entity.GetType().GetProperty("ID") ??
                                         entity.GetType().GetProperty("Guid") ??
                                         entity.GetType().GetProperty("Key");
                            if (idProp != null)
                            {
                                context.EntityId = idProp.GetValue(entity)?.ToString();
                            }
                            break;
                        }
                    }
                    catch
                    {
                        // Property-Zugriff fehlgeschlagen
                    }
                }
            }
        }

        /// <summary>
        /// Extrahiert verwandte Werte aus dem DataContext.
        /// </summary>
        private void ExtractRelatedValues(FrameworkElement element, ValidationContext context)
        {
            var dataContext = element.DataContext;
            if (dataContext == null)
                return;

            // Hole alle numerischen Properties
            var properties = dataContext.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.Name == context.FieldName)
                    continue;

                try
                {
                    var value = prop.GetValue(dataContext);
                    
                    // Nur numerische oder relevante Werte sammeln
                    if (value != null && IsRelevantType(value.GetType()))
                    {
                        context.RelatedValues[prop.Name] = value;
                    }
                }
                catch
                {
                    // Property-Zugriff fehlgeschlagen
                }
            }

            // Begrenze die Anzahl
            if (context.RelatedValues.Count > 20)
            {
                context.RelatedValues = context.RelatedValues
                    .Take(20)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        /// <summary>
        /// Leitet die Kategorie aus dem Kontext ab.
        /// </summary>
        private string? DeriveCategory(FrameworkElement element)
        {
            // Aus View-Name (z.B. "BestellungView" -> "Bestellung")
            if (element.DataContext != null)
            {
                var typeName = element.DataContext.GetType().Name;
                if (typeName.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase))
                {
                    return typeName[..^"ViewModel".Length];
                }
                if (typeName.EndsWith("VM", StringComparison.OrdinalIgnoreCase))
                {
                    return typeName[..^"VM".Length];
                }
                return typeName;
            }

            // Aus View selbst
            DependencyObject? current = element;
            while (current != null)
            {
                if (current is UserControl uc)
                {
                    var name = uc.GetType().Name;
                    if (name.EndsWith("View", StringComparison.OrdinalIgnoreCase))
                    {
                        return name[..^"View".Length];
                    }
                    return name;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        /// <summary>
        /// Extrahiert Validierungs-Regeln die am Element definiert sind.
        /// </summary>
        private void ExtractValidationRules(FrameworkElement element, ValidationContext context)
        {
            // Suche nach Binding mit Validation-Rules
            DependencyProperty? boundProperty = element switch
            {
                TextBox => TextBox.TextProperty,
                ComboBox => ComboBox.SelectedValueProperty,
                DatePicker => DatePicker.SelectedDateProperty,
                _ => null
            };

            if (boundProperty == null)
                return;

            var bindingExpression = BindingOperations.GetBindingExpression(element, boundProperty);
            if (bindingExpression?.ParentBinding?.ValidationRules == null)
                return;

            // Suche nach Range-Validierung
            foreach (var rule in bindingExpression.ParentBinding.ValidationRules)
            {
                var ruleType = rule.GetType();
                
                // Versuche Min/Max Properties zu finden
                var minProp = ruleType.GetProperty("Minimum") ?? ruleType.GetProperty("Min");
                var maxProp = ruleType.GetProperty("Maximum") ?? ruleType.GetProperty("Max");

                if (minProp != null && minProp.GetValue(rule) is IConvertible minValue)
                {
                    context.ExpectedMin = Convert.ToDouble(minValue);
                }

                if (maxProp != null && maxProp.GetValue(rule) is IConvertible maxValue)
                {
                    context.ExpectedMax = Convert.ToDouble(maxValue);
                }
            }
        }

        /// <summary>
        /// Holt einen Property-Wert über einen Pfad.
        /// </summary>
        private object? GetPropertyValue(object obj, string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('.');
            object? current = obj;

            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                var prop = current.GetType().GetProperty(part);
                if (prop == null)
                    return null;

                try
                {
                    current = prop.GetValue(current);
                }
                catch
                {
                    return null;
                }
            }

            return current;
        }

        /// <summary>
        /// Versucht einen Property-Wert zu holen.
        /// </summary>
        private object? TryGetPropertyValue(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                return prop?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Prüft ob ein Typ primitiv ist.
        /// </summary>
        private bool IsPrimitiveType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(decimal) || 
                   type == typeof(DateTime);
        }

        /// <summary>
        /// Prüft ob ein Typ relevant für Kontext ist.
        /// </summary>
        private bool IsRelevantType(Type type)
        {
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid) ||
                   type.IsEnum;
        }
    }
}
