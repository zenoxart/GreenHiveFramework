using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Zenox.NET.Core.Common.Command;
using Zenox.Wpf.UI.Services;

namespace Zenox.Wpf.UI.MVVM.ViewModel;

public class AuthViewModel : INotifyPropertyChanged
{
	private readonly IAuthApiService _authService;
	private string _username = string.Empty;
	private string _password = string.Empty;
	private string _statusMessage = string.Empty;
	private int _selectedTabIndex = 0;
	private bool _isLoading = false;
	private bool _isAdminLogin = false;

	public event EventHandler<AuthSuccessEventArgs>? AuthenticationSuccessful;

	public AuthViewModel()
	{
		_authService = new AuthApiService();
		LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsLoading);
		RegisterCommand = new RelayCommand(async _ => await RegisterAsync(), _ => !IsLoading);
	}

	public string Username
	{
		get => _username;
		set
		{
			_username = value;
			OnPropertyChanged();
		}
	}

	public string Password
	{
		get => _password;
		set
		{
			_password = value;
			OnPropertyChanged();
		}
	}

	public string StatusMessage
	{
		get => _statusMessage;
		set
		{
			_statusMessage = value;
			OnPropertyChanged();
		}
	}

	public int SelectedTabIndex
	{
		get => _selectedTabIndex;
		set
		{
			_selectedTabIndex = value;
			OnPropertyChanged();
			ClearFields();
		}
	}

	public bool IsLoading
	{
		get => _isLoading;
		set
		{
			_isLoading = value;
			OnPropertyChanged();
		}
	}

	public ICommand LoginCommand { get; }
	public ICommand RegisterCommand { get; }

	private async Task LoginAsync()
	{
		if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
		{
			StatusMessage = "Bitte füllen Sie alle Felder aus";
			return;
		}

		IsLoading = true;
		StatusMessage = "Anmeldung läuft...";

		try
		{
			var request = new LoginRequest
			{
				Username = Username,
				Password = Password
			};

			var response = await _authService.LoginAsync(request);

			if (response.Success)
			{
				StatusMessage = $"Willkommen, {response.User?.Username}!";
				
				// Event auslösen für erfolgreiche Authentifizierung
				AuthenticationSuccessful?.Invoke(this, new AuthSuccessEventArgs
				{
					Token = response.Token,
					User = response.User
				});
			}
			else
			{
				StatusMessage = response.Message;
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"Verbindungsfehler: Ist die API gestartet?";
		}
		finally
		{
			IsLoading = false;
		}
	}

	private async Task RegisterAsync()
	{
		if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
		{
			StatusMessage = "Bitte füllen Sie alle Felder aus";
			return;
		}

		if (Password.Length < 6)
		{
			StatusMessage = "Passwort muss mindestens 6 Zeichen lang sein";
			return;
		}

		IsLoading = true;
		StatusMessage = "Registrierung läuft...";

		try
		{
			var request = new RegisterRequest
			{
				Username = Username,
				Email = $"{Username}@zenox.local", // Auto-generierte E-Mail
				Password = Password
			};

			var response = await _authService.RegisterAsync(request);

			if (response.Success)
			{
				StatusMessage = $"Registrierung erfolgreich! Willkommen, {response.User?.Username}!";
				
				// Event auslösen für erfolgreiche Authentifizierung
				AuthenticationSuccessful?.Invoke(this, new AuthSuccessEventArgs
				{
					Token = response.Token,
					User = response.User
				});
			}
			else
			{
				StatusMessage = response.Message;
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"Verbindungsfehler: Ist die API gestartet?";
		}
		finally
		{
			IsLoading = false;
		}
	}

	private void ClearFields()
	{
		Password = string.Empty;
		StatusMessage = string.Empty;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

public class AuthSuccessEventArgs : EventArgs
{
	public string? Token { get; set; }
	public UserDto? User { get; set; }
}
