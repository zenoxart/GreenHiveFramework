using System.Windows;

namespace Zenox.NET.Core.Common.Tutorial
{
    /// <summary>
    /// Ermöglicht einen Tag im Xaml hinzuzufügen, zur Erkennung im Visual Tree
    /// </summary>
    public static class TutorialHelper
    {
        public static readonly DependencyProperty TutorialKeyProperty =
            DependencyProperty.RegisterAttached(
                "TutorialKey",
                typeof(string),
                typeof(TutorialHelper),
                new PropertyMetadata(null));

        public static void SetTutorialKey(DependencyObject element, string value)
            => element.SetValue(TutorialKeyProperty, value);

        public static string GetTutorialKey(DependencyObject element)
            => (string)element.GetValue(TutorialKeyProperty);
    }
}