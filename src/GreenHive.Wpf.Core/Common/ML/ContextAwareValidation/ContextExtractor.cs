using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace GreenHive.NET.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Extrahiert automatisch Validierungskontext aus UI-Elementen
    /// </summary>
    public class ContextExtractor
    {
        /// <summary>
        /// Extrahiert Kontext aus einem UI-Element
        /// </summary>
        public ValidationContext? Extract(FrameworkElement element)
        {
            var context = new ValidationContext();

            if (!ExtractValue(element, context))
                return null;

            context.FieldName = DeriveFieldName(element, context);
            context.ViewName = FindViewName(element);
            ExtractDataContextInfo(element, context);
            ExtractRelatedValues(element, context);

            return context;
        }

        private bool ExtractValue(FrameworkElement element, ValidationContext context)
        {
            object? value = null;
            DependencyProperty? property = null;

            value = element switch
            {
                TextBox tb => (value: tb.Text, property: TextBox.TextProperty),
                TextBlock tb => (tb.Text, TextBlock.TextProperty),
                Label lbl => (lbl.Content, ContentControl.ContentProperty),
                ComboBox cb => (cb.SelectedValue ?? cb.SelectedItem, ComboBox.SelectedValueProperty),
                DatePicker dp => (dp.SelectedDate, DatePicker.SelectedDateProperty),
                ContentControl cc => (cc.Content, ContentControl.ContentProperty),
                _ => (null, null)
            } switch
            {
                (var v, var p) => (value = v, property = p)
            };

            if (property != null)
            {
                var binding = BindingOperations.GetBinding(element, property);
                if (binding?.Path?.Path != null)
                {
                    context.BindingPath = binding.Path.Path;
                    if (value == null && element.DataContext != null)
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

        private string? DeriveFieldName(FrameworkElement element, ValidationContext context)
        {
            if (!string.IsNullOrEmpty(context.BindingPath))
            {
                return context.BindingPath.Split('.').Last();
            }

            if (!string.IsNullOrEmpty(element.Name))
            {
                var name = element.Name;
                var suffixes = new[] { "TextBox", "Label", "ComboBox", "Text", "Lbl", "Txt" };
                foreach (var suffix in suffixes)
                {
                    if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return name[..^suffix.Length];
                    }
                }
                return name;
            }

            var nearbyLabel = FindNearbyLabel(element);
            if (nearbyLabel != null)
            {
                var text = nearbyLabel.Content?.ToString()?.TrimEnd(':', '*', ' ');
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return element.GetType().Name;
        }

        private Label? FindNearbyLabel(FrameworkElement element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            if (parent == null)
                return null;

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
            else if (parent is StackPanel stack)
            {
                var children = stack.Children.Cast<UIElement>().ToList();
                var index = children.IndexOf(element);
                if (index > 0 && children[index - 1] is Label label)
                    return label;
            }

            return null;
        }

        private string? FindViewName(FrameworkElement element)
        {
            var current = element as DependencyObject;
            while (current != null)
            {
                if (current is Window window)
                    return window.GetType().Name;

                if (current is UserControl userControl)
                    return userControl.GetType().Name;

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void ExtractDataContextInfo(FrameworkElement element, ValidationContext context)
        {
            var dataContext = element.DataContext;
            if (dataContext == null)
                return;

            context.ViewModelName = dataContext.GetType().Name;

            var entityProp = dataContext.GetType().GetProperty("Entity") ??
                             dataContext.GetType().GetProperty("Model") ??
                             dataContext.GetType().GetProperty("Item");

            if (entityProp != null)
            {
                var entity = entityProp.GetValue(dataContext);
                if (entity != null)
                {
                    context.EntityName = entity.GetType().Name;

                    var idProp = entity.GetType().GetProperty("Id") ??
                                 entity.GetType().GetProperty("ID");
                    if (idProp != null)
                    {
                        context.EntityId = idProp.GetValue(entity)?.ToString();
                    }
                }
            }
        }

        private void ExtractRelatedValues(FrameworkElement element, ValidationContext context)
        {
            var dataContext = element.DataContext;
            if (dataContext == null)
                return;

            var props = dataContext.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props.Take(20))
            {
                if (prop.Name == context.FieldName)
                    continue;

                try
                {
                    var value = prop.GetValue(dataContext);
                    if (value != null && IsValidatableType(value))
                    {
                        context.RelatedValues[prop.Name] = value;
                    }
                }
                catch { }
            }
        }

        private bool IsValidatableType(object value)
        {
            return value is IConvertible convertible &&
                   (convertible is int or long or float or double or decimal or string);
        }

        private object? GetPropertyValue(object obj, string path)
        {
            try
            {
                var parts = path.Split('.');
                var current = obj;

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
            catch
            {
                return null;
            }
        }
    }
}
