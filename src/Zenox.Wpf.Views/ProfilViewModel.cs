using System.Windows.Input;
using Zenox.Wpf.Core.Common.Command;
using Zenox.Wpf.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.Wpf.Views
{
    /// <summary>
    /// ViewModel für das Benutzerprofil.
    /// </summary>
    public class ProfilViewModel : NotificationObject
    {
        public ProfilViewModel()
        {
            InitializeCommands();
            LoadProfile();
        }

        private void InitializeCommands()
        {
            SaveProfileCommand = new RelayCommand(
                async _ => await SaveProfileAsync())
            { Name = "SaveProfile" };

            ChangeAvatarCommand = new RelayCommand(
                _ => ChangeAvatar())
            { Name = "ChangeAvatar" };

            ResetProfileCommand = new RelayCommand(
                _ => ResetProfile())
            { Name = "ResetProfile" };

            ChangePasswordCommand = new RelayCommand(
                _ => ChangePassword(),
                _ => !string.IsNullOrEmpty(CurrentPassword) && !string.IsNullOrEmpty(NewPassword))
            { Name = "ChangePassword" };
        }

        #region Profile Properties

        private string _displayName = string.Empty;
        /// <summary>
        /// Anzeigename des Benutzers.
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        private string _email = string.Empty;
        /// <summary>
        /// E-Mail-Adresse des Benutzers.
        /// </summary>
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        private string _username = string.Empty;
        /// <summary>
        /// Benutzername.
        /// </summary>
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        private string _department = string.Empty;
        /// <summary>
        /// Abteilung des Benutzers.
        /// </summary>
        public string Department
        {
            get => _department;
            set => SetProperty(ref _department, value);
        }

        private string _role = string.Empty;
        /// <summary>
        /// Rolle des Benutzers.
        /// </summary>
        public string Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        private string _phoneNumber = string.Empty;
        /// <summary>
        /// Telefonnummer des Benutzers.
        /// </summary>
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => SetProperty(ref _phoneNumber, value);
        }

        private string _location = string.Empty;
        /// <summary>
        /// Standort des Benutzers.
        /// </summary>
        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        private string _avatarPath = string.Empty;
        /// <summary>
        /// Pfad zum Avatar-Bild.
        /// </summary>
        public string AvatarPath
        {
            get => _avatarPath;
            set => SetProperty(ref _avatarPath, value);
        }

        private DateTime _memberSince = DateTime.Now;
        /// <summary>
        /// Datum seit dem der Benutzer Mitglied ist.
        /// </summary>
        public DateTime MemberSince
        {
            get => _memberSince;
            set => SetProperty(ref _memberSince, value);
        }

        private DateTime _lastLogin = DateTime.Now;
        /// <summary>
        /// Letzter Login.
        /// </summary>
        public DateTime LastLogin
        {
            get => _lastLogin;
            set => SetProperty(ref _lastLogin, value);
        }

        #endregion

        #region Notification Settings

        private bool _emailNotifications = true;
        /// <summary>
        /// E-Mail-Benachrichtigungen aktiviert.
        /// </summary>
        public bool EmailNotifications
        {
            get => _emailNotifications;
            set => SetProperty(ref _emailNotifications, value);
        }

        private bool _pushNotifications = true;
        /// <summary>
        /// Push-Benachrichtigungen aktiviert.
        /// </summary>
        public bool PushNotifications
        {
            get => _pushNotifications;
            set => SetProperty(ref _pushNotifications, value);
        }

        private bool _soundNotifications = false;
        /// <summary>
        /// Ton-Benachrichtigungen aktiviert.
        /// </summary>
        public bool SoundNotifications
        {
            get => _soundNotifications;
            set => SetProperty(ref _soundNotifications, value);
        }

        #endregion

        #region Privacy Settings

        private bool _showOnlineStatus = true;
        /// <summary>
        /// Online-Status anzeigen.
        /// </summary>
        public bool ShowOnlineStatus
        {
            get => _showOnlineStatus;
            set => SetProperty(ref _showOnlineStatus, value);
        }

        private bool _showLastSeen = true;
        /// <summary>
        /// Zuletzt gesehen anzeigen.
        /// </summary>
        public bool ShowLastSeen
        {
            get => _showLastSeen;
            set => SetProperty(ref _showLastSeen, value);
        }

        private bool _allowProfileViewing = true;
        /// <summary>
        /// Profil für andere sichtbar.
        /// </summary>
        public bool AllowProfileViewing
        {
            get => _allowProfileViewing;
            set => SetProperty(ref _allowProfileViewing, value);
        }

        #endregion

        #region Password Change Properties

        private string _currentPassword = string.Empty;
        /// <summary>
        /// Aktuelles Passwort.
        /// </summary>
        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value);
        }

        private string _newPassword = string.Empty;
        /// <summary>
        /// Neues Passwort.
        /// </summary>
        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        private string _confirmPassword = string.Empty;
        /// <summary>
        /// Passwort bestätigen.
        /// </summary>
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        #endregion

        #region Status Properties

        private string _statusMessage = string.Empty;
        /// <summary>
        /// Status-Nachricht.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Initialen des Benutzers für Avatar-Fallback.
        /// </summary>
        public string Initials
        {
            get
            {
                if (string.IsNullOrEmpty(DisplayName))
                    return "?";

                var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();

                return DisplayName.Length >= 2 
                    ? DisplayName[..2].ToUpper() 
                    : DisplayName.ToUpper();
            }
        }

        #endregion

        #region Commands

        public ICommand SaveProfileCommand { get; private set; } = null!;
        public ICommand ChangeAvatarCommand { get; private set; } = null!;
        public ICommand ResetProfileCommand { get; private set; } = null!;
        public ICommand ChangePasswordCommand { get; private set; } = null!;

        #endregion

        #region Methods

        private void LoadProfile()
        {
            // Demo-Daten laden
            DisplayName = Environment.UserName;
            Username = Environment.UserName;
            Email = $"{Environment.UserName.ToLower()}@example.com";
            Department = "Entwicklung";
            Role = "Benutzer";
            Location = "Deutschland";
            MemberSince = DateTime.Now.AddYears(-1);
            LastLogin = DateTime.Now;
        }

        private async Task SaveProfileAsync()
        {
            await Task.Delay(500); // Simuliere Speichervorgang
            StatusMessage = "? Profil gespeichert";
            OnPropertyChanged(nameof(Initials));
        }

        private void ChangeAvatar()
        {
            // Hier könnte ein Datei-Dialog geöffnet werden
            StatusMessage = "Avatar-Änderung wird vorbereitet...";
        }

        private void ResetProfile()
        {
            LoadProfile();
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            StatusMessage = "? Profil zurückgesetzt";
        }

        private void ChangePassword()
        {
            if (NewPassword != ConfirmPassword)
            {
                StatusMessage = "? Passwörter stimmen nicht überein";
                return;
            }

            if (NewPassword.Length < 8)
            {
                StatusMessage = "? Passwort muss mindestens 8 Zeichen lang sein";
                return;
            }

            // Passwort ändern...
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            StatusMessage = "? Passwort erfolgreich geändert";
        }

        #endregion
    }
}
