using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GreenHive.NET.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Einfaches Behavior für AI-Validierung mit Rechtsklick
    /// </summary>
    public static class ValidationBehavior
    {
        #region Attached Properties

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ValidationBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.RegisterAttached(
                "FieldName",
                typeof(string),
                typeof(ValidationBehavior));

        public static readonly DependencyProperty FeedbackOptionsProperty =
            DependencyProperty.RegisterAttached(
                "FeedbackOptions",
                typeof(ValidationFeedbackOptions),
                typeof(ValidationBehavior),
                new PropertyMetadata(ValidationFeedbackOptions.Default));

        public static bool GetIsEnabled(DependencyObject obj) =>
            (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) =>
            obj.SetValue(IsEnabledProperty, value);

        public static string? GetFieldName(DependencyObject obj) =>
            (string?)obj.GetValue(FieldNameProperty);

        public static void SetFieldName(DependencyObject obj, string? value) =>
            obj.SetValue(FieldNameProperty, value);

        public static ValidationFeedbackOptions GetFeedbackOptions(DependencyObject obj) =>
            (ValidationFeedbackOptions)obj.GetValue(FeedbackOptionsProperty);

        public static void SetFeedbackOptions(DependencyObject obj, ValidationFeedbackOptions value) =>
            obj.SetValue(FeedbackOptionsProperty, value);

        #endregion

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
                return;

            if ((bool)e.NewValue)
            {
                element.ContextMenuOpening += OnContextMenuOpening;
            }
            else
            {
                element.ContextMenuOpening -= OnContextMenuOpening;
            }
        }

        private static void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            element.ContextMenu ??= new ContextMenu();

            if (!HasValidationMenuItem(element.ContextMenu))
            {
                if (element.ContextMenu.Items.Count > 0)
                {
                    element.ContextMenu.Items.Add(new Separator { Tag = "AIValidationSeparator" });
                }

                var menuItem = new MenuItem
                {
                    Header = "?? AI Validierung",
                    Tag = "AIValidation",
                    FontWeight = FontWeights.SemiBold
                };

                menuItem.Click += async (s, args) => await ValidateElementAsync(element);

                element.ContextMenu.Items.Add(menuItem);
            }
        }

        private static bool HasValidationMenuItem(ContextMenu menu)
        {
            foreach (var item in menu.Items)
            {
                if (item is MenuItem mi && mi.Tag as string == "AIValidation")
                    return true;
            }
            return false;
        }

        private static async Task ValidateElementAsync(FrameworkElement element)
        {
            var extractor = new ContextExtractor();
            var context = extractor.Extract(element);

            if (context == null || (context.Value == null && string.IsNullOrEmpty(context.ValueAsString)))
            {
                return;
            }

            var fieldName = GetFieldName(element);
            if (!string.IsNullOrEmpty(fieldName))
            {
                context.FieldName = fieldName;
            }

            var feedbackOptions = GetFeedbackOptions(element);

            await ValidationService.Instance.ValidateAsync(context, element, feedbackOptions);
        }
    }
}
