using System;
using System.Windows;

namespace GreenHive.Wpf.UI.Common.Localization
{
    /// <summary>
    /// Helper-Klasse zum einfachen Zugriff auf lokalisierte Strings
    /// </summary>
    public static class LocalizationHelper
    {
        /// <summary>
        /// Gibt einen lokalisierten String zurück
        /// </summary>
        /// <param name="key">Der Resource-Key</param>
        /// <param name="fallback">Fallback-Text falls Key nicht gefunden wird</param>
        /// <returns>Lokalisierter String oder Fallback</returns>
        public static string GetString(string key, string fallback = null)
        {
            try
            {
                var app = Application.Current;
                if (app != null)
                {
                    var resource = app.FindResource(key);
                    if (resource is string stringResource)
                    {
                        return stringResource;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lokalisierungs-Key nicht gefunden: {key} - {ex.Message}");
            }

            // Fallback: Verwende den Key selbst oder den angegebenen Fallback
            return fallback ?? key;
        }

        /// <summary>
        /// Prüft ob ein Key existiert
        /// </summary>
        public static bool KeyExists(string key)
        {
            try
            {
                var app = Application.Current;
                if (app != null)
                {
                    var resource = app.FindResource(key);
                    return resource != null;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
