using System.Windows;

namespace Zenox.NET.Core.Common.Permission
{
    /// <summary>
    /// Stellt ein Verhalten bereit, um Berechtigungen an UI-Elemente zu binden.
    /// </summary>
    public static class PermissionBehavior
    {
        /// <summary>
        /// Abhängigkeitseigenschaft für die erforderliche Berechtigung.
        /// </summary>
        public static readonly DependencyProperty RequiredPermissionProperty =
            DependencyProperty.RegisterAttached(
                "RequiredPermission",
                typeof(PermissionType),
                typeof(PermissionBehavior),
                new PropertyMetadata(PermissionType.None, null!));

        /// <summary>
        /// Ruft die erforderliche Berechtigung für ein UI-Element ab.
        /// </summary>
        /// <param name="element">Das UI-Element.</param>
        /// <returns>Die erforderliche Berechtigung.</returns>
        public static PermissionType GetRequiredPermission(UIElement element)
        {
            return (PermissionType)element.GetValue(RequiredPermissionProperty);
        }

        /// <summary>
        /// Legt die erforderliche Berechtigung für ein UI-Element fest.
        /// </summary>
        /// <param name="element">Das UI-Element.</param>
        /// <param name="value">Die festzulegende Berechtigung.</param>
        public static void SetRequiredPermission(UIElement element, PermissionType value)
        {
            element.SetValue(RequiredPermissionProperty, value);
        }
    }
}