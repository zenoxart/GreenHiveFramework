using System.Windows;
using Zenox.NET.Core.Common.MVVM.FactoryInjection;
using Zenox.NET.Core.Common.Services;

namespace Zenox.NET.Core.Common
{

    ///<summary>
    /// Stellt Methoden zum globalen Behandeln von Ausnahmen bereit.
    /// </summary>
    public static class ExceptionHandler 
    {
        /// <summary>
        /// Gibt den Logger an, der für das Protokollieren von Ausnahmen verwendet wird.
        /// </summary>
        public static AppLogManager Logger { get; set; } = new AppLogManager();

        /// <summary>
        /// Registriert globale Handler für nicht behandelte Ausnahmen in der Anwendung.
        /// </summary>
        public static void RegisterGlobalHandlers()
        {

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Logger?.LogException(ex, "AppDomain.UnhandledException");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Logger?.LogException(e.Exception, "UnobservedTaskException");
                e.SetObserved();
            };
        }
    }
}
