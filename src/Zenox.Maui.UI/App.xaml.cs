using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Zenox.Maui.UI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Globaler Exception-Handler
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

#if WINDOWS
            // Windows-spezifischer Exception-Handler
            Microsoft.UI.Xaml.Application.Current.UnhandledException += OnWindowsUnhandledException;
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Option 1: Shell-Navigation mit Tabs verwenden (empfohlen)
            return new Window(new AppShell());
            
            // Option 2: Direkt TabbedPage verwenden (alternativ)
            // return new Window(new MainPage());
        }

        /// <summary>
        /// Behandelt unbehandelte Exceptions im AppDomain
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                LogException("AppDomain.UnhandledException", exception);
                
                // Versuche die App zu retten
                if (!e.IsTerminating)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayExceptionAlert(exception);
                    });
                }
            }
        }

        /// <summary>
        /// Behandelt unbeobachtete Task-Exceptions
        /// </summary>
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("TaskScheduler.UnobservedTaskException", e.Exception);
            
            // Exception als beobachtet markieren, um App-Crash zu verhindern
            e.SetObserved();
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayExceptionAlert(e.Exception);
            });
        }

#if WINDOWS
        /// <summary>
        /// Windows-spezifischer Exception-Handler
        /// </summary>
        private void OnWindowsUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogException("Windows.UnhandledException", e.Exception);
            
            // Exception als behandelt markieren
            e.Handled = true;
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayExceptionAlert(e.Exception);
            });
        }
#endif

        /// <summary>
        /// Loggt eine Exception mit Details
        /// </summary>
        private void LogException(string source, Exception exception)
        {
            Debug.WriteLine($"========================================");
            Debug.WriteLine($"EXCEPTION in {source}");
            Debug.WriteLine($"========================================");
            Debug.WriteLine($"Type: {exception.GetType().FullName}");
            Debug.WriteLine($"Message: {exception.Message}");
            Debug.WriteLine($"StackTrace:\n{exception.StackTrace}");
            
            if (exception.InnerException != null)
            {
                Debug.WriteLine($"\nInner Exception:");
                Debug.WriteLine($"Type: {exception.InnerException.GetType().FullName}");
                Debug.WriteLine($"Message: {exception.InnerException.Message}");
                Debug.WriteLine($"StackTrace:\n{exception.InnerException.StackTrace}");
            }
            
            Debug.WriteLine($"========================================");
        }

        /// <summary>
        /// Zeigt dem Benutzer eine Exception-Meldung an
        /// </summary>
        private async Task DisplayExceptionAlert(Exception exception)
        {
            try
            {
                if (MainPage != null)
                {
                    await MainPage.DisplayAlert(
                        "Fehler",
                        $"Es ist ein Fehler aufgetreten:\n\n{exception.Message}\n\nTyp: {exception.GetType().Name}",
                        "OK");
                }
            }
            catch
            {
                // Wenn das Alert selbst fehlschlägt, ignorieren wir es
                Debug.WriteLine("Konnte Alert nicht anzeigen");
            }
        }
    }
}