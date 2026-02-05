using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zenox.NET.Core.Common.ML.ContextAwareValidation
{
    /// <summary>
    /// Globaler Manager für automatische AI-Validierung über Kontextmenü
    /// </summary>
    public class GlobalValidationManager
    {
        private static GlobalValidationManager? _instance;
        private static readonly object _lock = new();

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

        private readonly HashSet<Window> _registeredWindows = new();

        public bool IsEnabled { get; set; } = true;

        public HashSet<Type> MonitoredTypes { get; } = new()
        {
            typeof(TextBox),
            typeof(TextBlock),
            typeof(Label),
            typeof(ComboBox)
        };

        private GlobalValidationManager()
        {
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
        /// Initialisiert den Manager und registriert alle Fenster
        /// </summary>
        public void Initialize()
        {
            if (Application.Current?.Windows == null)
                return;

            foreach (Window window in Application.Current.Windows)
            {
                RegisterWindow(window);
            }
        }

        /// <summary>
        /// Registriert ein Fenster für automatische Validierung
        /// </summary>
        public void RegisterWindow(Window window)
        {
            if (_registeredWindows.Contains(window) || !IsEnabled)
                return;

            _registeredWindows.Add(window);
            window.Loaded += Window_Loaded;
            window.Closed += Window_Closed;

            if (window.IsLoaded)
            {
                AttachToVisualTree(window);
            }
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
                _registeredWindows.Remove(window);
                window.Loaded -= Window_Loaded;
                window.Closed -= Window_Closed;
            }
        }

        private void AttachToVisualTree(DependencyObject root)
        {
            if (root is FrameworkElement element && ShouldMonitor(element))
            {
                ValidationBehavior.SetIsEnabled(element, true);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                AttachToVisualTree(child);
            }
        }

        private bool ShouldMonitor(FrameworkElement element)
        {
            if (!MonitoredTypes.Any(t => t.IsInstanceOfType(element)))
                return false;

            if (!string.IsNullOrEmpty(element.Name) &&
                element.Name.StartsWith("PART_", StringComparison.OrdinalIgnoreCase))
                return false;

            return element.DataContext != null;
        }
    }
}
