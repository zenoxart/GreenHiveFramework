using System.Windows;
using System.Windows.Controls;
using Zenox.Wpf.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.Core.Common.Permission
{
    /// <summary>
    /// Definiert die verschiedenen Berechtigungen.
    /// </summary>
    public enum PermissionType
    {
        None,
        User,
        Developer,
        Maintainer,
        Admin
    }

    /// <summary>
    /// Verwaltet Berechtigungen und bietet Methoden zur Überprüfung von Berechtigungen.
    /// </summary>
    public class PermissionManager : NotificationObject
    {
        static PermissionManager()
        {
        }

        /// <summary>
        /// Represents the set of permissions that have been granted.
        /// </summary>
        /// <remarks>This collection is used to track the permissions currently granted within the
        /// application. It is initialized as an empty set and can be modified as permissions are added or
        /// removed.</remarks>
        protected static PermissionType _aktuelleBerechtigung;

        /// <summary>
        /// Fügt eine Berechtigung hinzu.
        /// </summary>
        /// <param name="berechtigung">Die hinzuzufügende Berechtigung.</param>
        public static void GrantPermission(PermissionType berechtigung)
        {
            _aktuelleBerechtigung = berechtigung;
        }

        public static PermissionType CurrentPermission()
        {
            return _aktuelleBerechtigung;
        }

        /// <summary>
        /// Entfernt eine Berechtigung.
        /// </summary>
        public static void RevokePermission()
        {
            _aktuelleBerechtigung = PermissionType.None;
        }

        /// <summary>
        /// Überprüft, ob eine bestimmte Berechtigung vorhanden ist.
        /// </summary>
        /// <param name="permission"></param>
        /// <returns></returns>
        public static bool HasPermission(PermissionType permission)
        {
            return _aktuelleBerechtigung >= permission;
        }
    }

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