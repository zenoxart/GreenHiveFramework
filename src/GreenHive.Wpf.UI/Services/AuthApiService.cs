using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace GreenHive.Wpf.UI.Services;

public class LoginRequest
{
	public string Username { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
	public string Username { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
}

public class AuthResponse
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public string? Token { get; set; }
	public UserDto? User { get; set; }
}

public class UserDto
{
	public int Id { get; set; }
	public string Username { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public DateTime CreatedAt { get; set; }
}

public interface IAuthApiService
{
	Task<AuthResponse> LoginAsync(LoginRequest request);
	Task<AuthResponse> RegisterAsync(RegisterRequest request);
}

public class AuthApiService : IAuthApiService
{
	private readonly HttpClient _httpClient;
	private readonly string _baseUrl;

	public AuthApiService()
	{
		_httpClient = new HttpClient();
		_baseUrl = "https://localhost:7183/api"; // Passen Sie die URL an Ihre API an
	}

	public AuthApiService(string baseUrl)
	{
		_httpClient = new HttpClient();
		_baseUrl = baseUrl;
	}

	public async Task<AuthResponse> LoginAsync(LoginRequest request)
	{
		try
		{
			var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/Auth/login", request);

			if (response.IsSuccessStatusCode)
			{
				var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
				return result ?? new AuthResponse { Success = false, Message = "Fehler beim Verarbeiten der Antwort" };
			}

			var errorContent = await response.Content.ReadAsStringAsync();
			try
			{
				var errorResponse = JsonSerializer.Deserialize<AuthResponse>(errorContent);
				return errorResponse ?? new AuthResponse { Success = false, Message = "Anmeldung fehlgeschlagen" };
			}
			catch
			{
				return new AuthResponse { Success = false, Message = $"Anmeldung fehlgeschlagen: {response.StatusCode}" };
			}
		}
		catch (HttpRequestException ex)
		{
			return new AuthResponse { Success = false, Message = $"Verbindungsfehler: {ex.Message}" };
		}
		catch (Exception ex)
		{
			return new AuthResponse { Success = false, Message = $"Fehler: {ex.Message}" };
		}
	}

	public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
	{
		try
		{
			var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/Auth/register", request);

			if (response.IsSuccessStatusCode)
			{
				var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
				return result ?? new AuthResponse { Success = false, Message = "Fehler beim Verarbeiten der Antwort" };
			}

			var errorContent = await response.Content.ReadAsStringAsync();
			try
			{
				var errorResponse = JsonSerializer.Deserialize<AuthResponse>(errorContent);
				return errorResponse ?? new AuthResponse { Success = false, Message = "Registrierung fehlgeschlagen" };
			}
			catch
			{
				return new AuthResponse { Success = false, Message = $"Registrierung fehlgeschlagen: {response.StatusCode}" };
			}
		}
		catch (HttpRequestException ex)
		{
			return new AuthResponse { Success = false, Message = $"Verbindungsfehler: {ex.Message}" };
		}
		catch (Exception ex)
		{
			return new AuthResponse { Success = false, Message = $"Fehler: {ex.Message}" };
		}
	}
}
