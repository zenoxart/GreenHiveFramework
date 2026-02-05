using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using Zenox.NET.Core.Common.Command;
using Zenox.NET.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.Views.Hilfe
{
    /// <summary>
    /// ViewModel für die About-View, die Informationen über die Anwendung bereitstellt.
    /// </summary>
    public class AboutViewModel : NotificationObject
    {
        public AboutViewModel()
        {
            LoadApplicationInfo();
            LoadSystemInfo();
            InitializeCommands();
        }

        #region Properties

        private string _applicationName = "Zenox WPF Framework";
        public string ApplicationName
        {
            get => _applicationName;
            set => SetProperty(ref _applicationName, value);
        }

        private string _version = "1.0.0";
        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        private string _description = "Ein modernes MVVM WPF Framework mit erweiterten Funktionen";
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _copyright = "© 2024 Daniel Kasper. Alle Rechte vorbehalten.";
        public string Copyright
        {
            get => _copyright;
            set => SetProperty(ref _copyright, value);
        }

        private string _author = "Daniel Kasper";
        public string Author
        {
            get => _author;
            set => SetProperty(ref _author, value);
        }

        private string _frameworkVersion = ".NET 10";
        public string FrameworkVersion
        {
            get => _frameworkVersion;
            set => SetProperty(ref _frameworkVersion, value);
        }

        private string _buildDate = DateTime.Now.ToString("dd.MM.yyyy");
        public string BuildDate
        {
            get => _buildDate;
            set => SetProperty(ref _buildDate, value);
        }

        private string _website = "https://github.com/zenox";
        public string Website
        {
            get => _website;
            set => SetProperty(ref _website, value);
        }

        private string _operatingSystem = "";
        public string OperatingSystem
        {
            get => _operatingSystem;
            set => SetProperty(ref _operatingSystem, value);
        }

        private string _is64BitSystem = "";
        public string Is64BitSystem
        {
            get => _is64BitSystem;
            set => SetProperty(ref _is64BitSystem, value);
        }

        private string _processorCount = "";
        public string ProcessorCount
        {
            get => _processorCount;
            set => SetProperty(ref _processorCount, value);
        }

        private string _machineName = "";
        public string MachineName
        {
            get => _machineName;
            set => SetProperty(ref _machineName, value);
        }

        #endregion

        #region Commands

        public ICommand OpenWebsiteCommand { get; private set; } = null!;
        public ICommand OpenDocumentationCommand { get; private set; } = null!;
        public ICommand OpenLicenseCommand { get; private set; } = null!;
        public ICommand CheckForUpdatesCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            OpenWebsiteCommand = new RelayCommand(OpenWebsite);
            OpenDocumentationCommand = new RelayCommand(OpenDocumentation);
            OpenLicenseCommand = new RelayCommand(OpenLicense);
            CheckForUpdatesCommand = new RelayCommand(CheckForUpdates);
        }

        private void OpenWebsite(object? parameter)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Website,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Öffnen der Website: {ex.Message}");
            }
        }

        private void OpenDocumentation(object? parameter)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/zenox/documentation",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Öffnen der Dokumentation: {ex.Message}");
            }
        }

        private void OpenLicense(object? parameter)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/zenox/license",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Öffnen der Lizenz: {ex.Message}");
            }
        }

        private void CheckForUpdates(object? parameter)
        {
            // Hier könnte eine tatsächliche Update-Prüfung implementiert werden
            Debug.WriteLine("Prüfe auf Updates...");
        }

        #endregion

        #region Methods

        private void LoadApplicationInfo()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                if (assembly != null)
                {
                    // Version
                    var version = assembly.GetName().Version;
                    if (version != null)
                    {
                        Version = $"{version.Major}.{version.Minor}.{version.Build}";
                    }

                    // Build-Datum aus Assembly
                    var buildDateAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (buildDateAttribute != null)
                    {
                        BuildDate = DateTime.Now.ToString("dd.MM.yyyy");
                    }

                    // Weitere Metadaten
                    var titleAttribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
                    if (titleAttribute != null && !string.IsNullOrEmpty(titleAttribute.Title))
                    {
                        ApplicationName = titleAttribute.Title;
                    }

                    var descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
                    if (descriptionAttribute != null && !string.IsNullOrEmpty(descriptionAttribute.Description))
                    {
                        Description = descriptionAttribute.Description;
                    }

                    var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
                    if (copyrightAttribute != null && !string.IsNullOrEmpty(copyrightAttribute.Copyright))
                    {
                        Copyright = copyrightAttribute.Copyright;
                    }
                }

                // Framework-Version
                FrameworkVersion = $".NET {Environment.Version.Major}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Laden der Anwendungsinformationen: {ex.Message}");
            }
        }

        private void LoadSystemInfo()
        {
            try
            {
                OperatingSystem = Environment.OSVersion.ToString();
                Is64BitSystem = Environment.Is64BitOperatingSystem ? "Ja" : "Nein";
                ProcessorCount = Environment.ProcessorCount.ToString();
                MachineName = Environment.MachineName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Laden der Systeminformationen: {ex.Message}");
            }
        }

        #endregion
    }
}
