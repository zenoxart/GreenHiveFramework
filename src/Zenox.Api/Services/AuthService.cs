using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Zenox.Api.Data;
using Zenox.Api.Models;
using Zenox.Api.Models.DTOs;

namespace Zenox.Api.Services;

public interface IAuthService
{
	Task<AuthResponse> RegisterAsync(RegisterRequest request);
	Task<AuthResponse> LoginAsync(LoginRequest request);
	string GenerateJwtToken(User user);
}

public class AuthService : IAuthService
{
	private readonly ApplicationDbContext _context;
	private readonly IConfiguration _configuration;

	public AuthService(ApplicationDbContext context, IConfiguration configuration)
	{
		_context = context;
		_configuration = configuration;
	}

	public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
	{
		if (await _context.Users.AnyAsync(u => u.Username == request.Username))
		{
			return new AuthResponse
			{
				Success = false,
				Message = "Benutzername ist bereits vergeben"
			};
		}

		// E-Mail-Prüfung nur wenn E-Mail angegeben wurde
		if (!string.IsNullOrEmpty(request.Email) && 
			await _context.Users.AnyAsync(u => u.Email == request.Email))
		{
			return new AuthResponse
			{
				Success = false,
				Message = "E-Mail-Adresse ist bereits registriert"
			};
		}

		var user = new User
		{
			Username = request.Username,
			Email = request.Email ?? $"{request.Username}@zenox.local",
			PasswordHash = HashPassword(request.Password),
			FirstName = request.FirstName,
			LastName = request.LastName,
			CreatedAt = DateTime.UtcNow,
			IsActive = true
		};


		_context.Users.Add(user);
		await _context.SaveChangesAsync();

		var token = GenerateJwtToken(user);

		return new AuthResponse
		{
			Success = true,
			Message = "Registrierung erfolgreich",
			Token = token,
			User = MapToUserDto(user)
		};
	}

	public async Task<AuthResponse> LoginAsync(LoginRequest request)
	{
		var user = await _context.Users
			.FirstOrDefaultAsync(u => u.Username == request.Username);

		if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
		{
			return new AuthResponse
			{
				Success = false,
				Message = "Ungültige Anmeldedaten"
			};
		}

		if (!user.IsActive)
		{
			return new AuthResponse
			{
				Success = false,
				Message = "Benutzerkonto ist deaktiviert"
			};
		}

		user.LastLoginAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		var token = GenerateJwtToken(user);

		return new AuthResponse
		{
			Success = true,
			Message = "Anmeldung erfolgreich",
			Token = token,
			User = MapToUserDto(user)
		};
	}

	public string GenerateJwtToken(User user)
	{
		var securityKey = new SymmetricSecurityKey(
			Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"));
		var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

		var claims = new List<Claim>
		{
			new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
			new Claim(ClaimTypes.Name, user.Username),
			new Claim(ClaimTypes.Email, user.Email)
		};

		if (user.IsAdmin)
		{
			claims.Add(new Claim(ClaimTypes.Role, "Admin"));
		}

		var token = new JwtSecurityToken(
			issuer: _configuration["Jwt:Issuer"] ?? "ZenoxApi",
			audience: _configuration["Jwt:Audience"] ?? "ZenoxClient",
			claims: claims.ToArray(),
			expires: DateTime.UtcNow.AddHours(24),
			signingCredentials: credentials
		);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	private static string HashPassword(string password)
	{
		using var sha256 = SHA256.Create();
		var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
		return Convert.ToBase64String(bytes);
	}

	private static bool VerifyPassword(string password, string hash)
	{
		var hashedPassword = HashPassword(password);
		return hashedPassword == hash;
	}

	private static UserDto MapToUserDto(User user)
	{
		return new UserDto
		{
			Id = user.Id,
			Username = user.Username,
			Email = user.Email,
			FirstName = user.FirstName,
			LastName = user.LastName,
			CreatedAt = user.CreatedAt
		};
	}
}
