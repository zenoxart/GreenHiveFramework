using GreenHive.NET.Core.Common.MVVM.ViewModelSupport;

namespace GreenHive.NET.Core.Common.Permission
{
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
}
