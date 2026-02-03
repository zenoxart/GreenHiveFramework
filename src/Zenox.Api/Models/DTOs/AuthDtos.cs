using System.ComponentModel.DataAnnotations;

namespace Zenox.Api.Models.DTOs;

public class LoginRequest
{
	[Required]
	public string Username { get; set; } = string.Empty;

	[Required]
	public string Password { get; set; } = string.Empty;

	public bool IsAdminLogin { get; set; } = false;
}

public class RegisterRequest
{
	[Required]
	[StringLength(100, MinimumLength = 3)]
	public string Username { get; set; } = string.Empty;

	[EmailAddress]
	public string? Email { get; set; }

	[Required]
	[StringLength(100, MinimumLength = 6)]
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

