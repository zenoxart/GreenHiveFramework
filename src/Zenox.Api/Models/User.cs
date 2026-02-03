using System.ComponentModel.DataAnnotations;

namespace Zenox.Api.Models;

public class User
{
	[Key]
	public int Id { get; set; }

	[Required]
	[StringLength(100)]
	public string Username { get; set; } = string.Empty;

	[Required]
	[EmailAddress]
	[StringLength(255)]
	public string Email { get; set; } = string.Empty;

	[Required]
	public string PasswordHash { get; set; } = string.Empty;

	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? LastLoginAt { get; set; }

	public bool IsActive { get; set; } = true;

	public bool IsAdmin { get; set; } = false;
}
