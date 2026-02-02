using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Zenox.Wpf.Core.Common.Command;
using Zenox.Wpf.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.Views
{
    /// <summary>
    /// ViewModel für das Dashboard.
    /// </summary>
    public class DashboardViewModel : NotificationObject
    {
        public DashboardViewModel()
        {
            RecentActivities = [];
            Notifications = [];
            UpcomingEvents = [];
            ProjectProgress = [];

            InitializeCommands();
            LoadDashboardData();
        }

        private void InitializeCommands()
        {
            RefreshCommand = new RelayCommand(
                async _ => await RefreshDataAsync())
            { Name = "RefreshDashboard" };

            NewTaskCommand = new RelayCommand(
                _ => CreateNewTask())
            { Name = "NewTask" };

            OpenReportsCommand = new RelayCommand(
                _ => OpenReports())
            { Name = "OpenReports" };

            OpenTeamCommand = new RelayCommand(
                _ => OpenTeam())
            { Name = "OpenTeam" };

            OpenDocumentsCommand = new RelayCommand(
                _ => OpenDocuments())
            { Name = "OpenDocuments" };

            OpenSettingsCommand = new RelayCommand(
                _ => OpenSettings())
            { Name = "OpenSettings" };

            OpenHelpCommand = new RelayCommand(
                _ => OpenHelp())
            { Name = "OpenHelp" };

            ViewAllActivitiesCommand = new RelayCommand(
                _ => ViewAllActivities())
            { Name = "ViewAllActivities" };

            MarkAllReadCommand = new RelayCommand(
                _ => MarkAllNotificationsRead())
            { Name = "MarkAllRead" };
        }

        #region Header Properties

        /// <summary>
        /// Willkommensnachricht mit Benutzernamen.
        /// </summary>
        public string WelcomeMessage => $"Willkommen zurück, {Environment.UserName}!";

        private DateTime _currentDateTime = DateTime.Now;
        /// <summary>
        /// Aktuelles Datum und Uhrzeit.
        /// </summary>
        public DateTime CurrentDateTime
        {
            get => _currentDateTime;
            set => SetProperty(ref _currentDateTime, value);
        }

        #endregion

        #region Statistics Properties

        private int _activeUsersCount = 0;
        /// <summary>
        /// Anzahl aktiver Benutzer.
        /// </summary>
        public int ActiveUsersCount
        {
            get => _activeUsersCount;
            set => SetProperty(ref _activeUsersCount, value);
        }

        private double _activeUsersChange = 0;
        /// <summary>
        /// Änderung der aktiven Benutzer in Prozent.
        /// </summary>
        public double ActiveUsersChange
        {
            get => _activeUsersChange;
            set => SetProperty(ref _activeUsersChange, value);
        }

        private int _completedTasksCount = 0;
        /// <summary>
        /// Anzahl erledigter Aufgaben.
        /// </summary>
        public int CompletedTasksCount
        {
            get => _completedTasksCount;
            set => SetProperty(ref _completedTasksCount, value);
        }

        private double _completedTasksChange = 0;
        /// <summary>
        /// Änderung der erledigten Aufgaben in Prozent.
        /// </summary>
        public double CompletedTasksChange
        {
            get => _completedTasksChange;
            set => SetProperty(ref _completedTasksChange, value);
        }

        private int _pendingTasksCount = 0;
        /// <summary>
        /// Anzahl offener Aufgaben.
        /// </summary>
        public int PendingTasksCount
        {
            get => _pendingTasksCount;
            set => SetProperty(ref _pendingTasksCount, value);
        }

        private string _pendingTasksInfo = string.Empty;
        /// <summary>
        /// Info zu offenen Aufgaben.
        /// </summary>
        public string PendingTasksInfo
        {
            get => _pendingTasksInfo;
            set => SetProperty(ref _pendingTasksInfo, value);
        }

        private int _systemHealthPercent = 0;
        /// <summary>
        /// Systemgesundheit in Prozent.
        /// </summary>
        public int SystemHealthPercent
        {
            get => _systemHealthPercent;
            set => SetProperty(ref _systemHealthPercent, value);
        }

        private string _systemHealthStatus = string.Empty;
        /// <summary>
        /// Systemstatus-Text.
        /// </summary>
        public string SystemHealthStatus
        {
            get => _systemHealthStatus;
            set => SetProperty(ref _systemHealthStatus, value);
        }

        #endregion

        #region Notifications Properties

        private int _unreadNotificationsCount = 0;
        /// <summary>
        /// Anzahl ungelesener Benachrichtigungen.
        /// </summary>
        public int UnreadNotificationsCount
        {
            get => _unreadNotificationsCount;
            set
            {
                SetProperty(ref _unreadNotificationsCount, value);
                OnPropertyChanged(nameof(HasUnreadNotifications));
            }
        }

        /// <summary>
        /// Gibt an, ob ungelesene Benachrichtigungen vorhanden sind.
        /// </summary>
        public bool HasUnreadNotifications => UnreadNotificationsCount > 0;

        #endregion

        #region Collections

        /// <summary>
        /// Liste der letzten Aktivitäten.
        /// </summary>
        public ObservableCollection<ActivityItem> RecentActivities { get; }

        /// <summary>
        /// Liste der Benachrichtigungen.
        /// </summary>
        public ObservableCollection<NotificationItem> Notifications { get; }

        /// <summary>
        /// Liste der anstehenden Termine.
        /// </summary>
        public ObservableCollection<EventItem> UpcomingEvents { get; }

        /// <summary>
        /// Liste der Projektfortschritte.
        /// </summary>
        public ObservableCollection<ProjectProgressItem> ProjectProgress { get; }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand NewTaskCommand { get; private set; } = null!;
        public ICommand OpenReportsCommand { get; private set; } = null!;
        public ICommand OpenTeamCommand { get; private set; } = null!;
        public ICommand OpenDocumentsCommand { get; private set; } = null!;
        public ICommand OpenSettingsCommand { get; private set; } = null!;
        public ICommand OpenHelpCommand { get; private set; } = null!;
        public ICommand ViewAllActivitiesCommand { get; private set; } = null!;
        public ICommand MarkAllReadCommand { get; private set; } = null!;

        #endregion

        #region Data Loading

        private void LoadDashboardData()
        {
            // Statistiken laden
            ActiveUsersCount = 247;
            ActiveUsersChange = 12.5;
            CompletedTasksCount = 1842;
            CompletedTasksChange = 8.3;
            PendingTasksCount = 23;
            PendingTasksInfo = "5 dringend";
            SystemHealthPercent = 98;
            SystemHealthStatus = "Alle Systeme operativ";

            // Aktivitäten laden
            LoadRecentActivities();

            // Benachrichtigungen laden
            LoadNotifications();

            // Termine laden
            LoadUpcomingEvents();

            // Projektfortschritt laden
            LoadProjectProgress();
        }

        private void LoadRecentActivities()
        {
            RecentActivities.Clear();

            RecentActivities.Add(new ActivityItem
            {
                Icon = "?",
                IconBackground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Title = "Aufgabe abgeschlossen",
                Description = "UI-Redesign für Dashboard wurde abgeschlossen",
                TimeAgo = "vor 5 Min."
            });

            RecentActivities.Add(new ActivityItem
            {
                Icon = "??",
                IconBackground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Title = "Neuer Kommentar",
                Description = "Max Mustermann hat einen Kommentar hinterlassen",
                TimeAgo = "vor 15 Min."
            });

            RecentActivities.Add(new ActivityItem
            {
                Icon = "??",
                IconBackground = new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                Title = "Neuer Benutzer",
                Description = "Anna Schmidt hat sich registriert",
                TimeAgo = "vor 1 Std."
            });

            RecentActivities.Add(new ActivityItem
            {
                Icon = "??",
                IconBackground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                Title = "Bericht generiert",
                Description = "Monatlicher Performance-Bericht verfügbar",
                TimeAgo = "vor 2 Std."
            });

            RecentActivities.Add(new ActivityItem
            {
                Icon = "??",
                IconBackground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                Title = "System-Update",
                Description = "Sicherheitsupdates wurden installiert",
                TimeAgo = "vor 3 Std."
            });
        }

        private void LoadNotifications()
        {
            Notifications.Clear();

            Notifications.Add(new NotificationItem
            {
                Title = "Neue Aufgabe zugewiesen",
                Message = "Sie haben eine neue Aufgabe von Thomas Mueller erhalten.",
                TypeColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                TimeAgo = "vor 10 Min."
            });

            Notifications.Add(new NotificationItem
            {
                Title = "Erinnerung: Meeting",
                Message = "Team-Meeting in 30 Minuten im Konferenzraum A.",
                TypeColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                TimeAgo = "vor 25 Min."
            });

            Notifications.Add(new NotificationItem
            {
                Title = "Projekt abgeschlossen",
                Message = "Das Projekt 'Website Relaunch' wurde erfolgreich abgeschlossen.",
                TypeColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                TimeAgo = "vor 1 Std."
            });

            UnreadNotificationsCount = 3;
        }

        private void LoadUpcomingEvents()
        {
            UpcomingEvents.Clear();

            var today = DateTime.Now;

            UpcomingEvents.Add(new EventItem
            {
                Title = "Team Standup",
                EventDate = today,
                Time = "09:00 - 09:30",
                DateBackground = new SolidColorBrush(Color.FromRgb(33, 150, 243))
            });

            UpcomingEvents.Add(new EventItem
            {
                Title = "Projektreview",
                EventDate = today.AddDays(1),
                Time = "14:00 - 15:30",
                DateBackground = new SolidColorBrush(Color.FromRgb(156, 39, 176))
            });

            UpcomingEvents.Add(new EventItem
            {
                Title = "Kundenpräsentation",
                EventDate = today.AddDays(3),
                Time = "10:00 - 12:00",
                DateBackground = new SolidColorBrush(Color.FromRgb(76, 175, 80))
            });
        }

        private void LoadProjectProgress()
        {
            ProjectProgress.Clear();

            ProjectProgress.Add(new ProjectProgressItem
            {
                Name = "Website Redesign",
                Percent = 85,
                ProgressColor = new SolidColorBrush(Color.FromRgb(76, 175, 80))
            });

            ProjectProgress.Add(new ProjectProgressItem
            {
                Name = "Mobile App Development",
                Percent = 62,
                ProgressColor = new SolidColorBrush(Color.FromRgb(33, 150, 243))
            });

            ProjectProgress.Add(new ProjectProgressItem
            {
                Name = "API Integration",
                Percent = 45,
                ProgressColor = new SolidColorBrush(Color.FromRgb(255, 152, 0))
            });

            ProjectProgress.Add(new ProjectProgressItem
            {
                Name = "Dokumentation",
                Percent = 30,
                ProgressColor = new SolidColorBrush(Color.FromRgb(156, 39, 176))
            });
        }

        #endregion

        #region Command Implementations

        private async Task RefreshDataAsync()
        {
            await Task.Delay(500); // Simuliere Ladevorgang
            CurrentDateTime = DateTime.Now;
            LoadDashboardData();
        }

        private void CreateNewTask()
        {
            // Navigation zur Aufgabenerstellung
        }

        private void OpenReports()
        {
            // Navigation zu Berichten
        }

        private void OpenTeam()
        {
            // Navigation zur Teamübersicht
        }

        private void OpenDocuments()
        {
            // Navigation zu Dokumenten
        }

        private void OpenSettings()
        {
            // Navigation zu Einstellungen
        }

        private void OpenHelp()
        {
            // Navigation zur Hilfe
        }

        private void ViewAllActivities()
        {
            // Navigation zur vollständigen Aktivitätsliste
        }

        private void MarkAllNotificationsRead()
        {
            UnreadNotificationsCount = 0;
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Repräsentiert eine Aktivität im Dashboard.
    /// </summary>
    public class ActivityItem
    {
        public string Icon { get; set; } = string.Empty;
        public Brush IconBackground { get; set; } = Brushes.Gray;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
    }

    /// <summary>
    /// Repräsentiert eine Benachrichtigung im Dashboard.
    /// </summary>
    public class NotificationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Brush TypeColor { get; set; } = Brushes.Blue;
        public string TimeAgo { get; set; } = string.Empty;
    }

    /// <summary>
    /// Repräsentiert einen Termin im Dashboard.
    /// </summary>
    public class EventItem
    {
        public string Title { get; set; } = string.Empty;
        public DateTime EventDate { get; set; } = DateTime.Now;
        public string Time { get; set; } = string.Empty;
        public Brush DateBackground { get; set; } = Brushes.Blue;

        public string DayOfMonth => EventDate.Day.ToString();
        public string MonthShort => EventDate.ToString("MMM").ToUpper();
    }

    /// <summary>
    /// Repräsentiert einen Projektfortschritt im Dashboard.
    /// </summary>
    public class ProjectProgressItem : NotificationObject
    {
        public string Name { get; set; } = string.Empty;

        private int _percent;
        public int Percent
        {
            get => _percent;
            set
            {
                _percent = value;
                OnPropertyChanged(nameof(Percent));
                OnPropertyChanged(nameof(ProgressWidth));
            }
        }

        public Brush ProgressColor { get; set; } = Brushes.Green;

        /// <summary>
        /// Berechnet die Breite des Fortschrittsbalkens.
        /// </summary>
        public GridLength ProgressWidth => new GridLength(Percent, GridUnitType.Star);
    }

    #endregion
}
