using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GreenHive.Api.Models.DTOs;
using GreenHive.Api.Services;

namespace GreenHive.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
	private readonly IAuthService _authService;
	private readonly ILogger<AuthController> _logger;

	public AuthController(IAuthService authService, ILogger<AuthController> logger)
	{
		_authService = authService;
		_logger = logger;
	}

	/// <summary>
	/// Registriert einen neuen Benutzer
	/// </summary>
	[HttpPost("register")]
	[AllowAnonymous]
	public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
	{
		try
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(new AuthResponse
				{
					Success = false,
					Message = "Ungültige Eingabedaten"
				});
			}

			var result = await _authService.RegisterAsync(request);

			if (!result.Success)
			{
				return BadRequest(result);
			}

			_logger.LogInformation("Neuer Benutzer registriert: {Username}", request.Username);
			return Ok(result);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Fehler bei der Registrierung");
			return StatusCode(500, new AuthResponse
			{
				Success = false,
				Message = "Ein Fehler ist aufgetreten"
			});
		}
	}

	/// <summary>
	/// Meldet einen Benutzer an
	/// </summary>
	[HttpPost("login")]
	[AllowAnonymous]
	public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
	{
		try
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(new AuthResponse
				{
					Success = false,
					Message = "Ungültige Eingabedaten"
				});
			}

			var result = await _authService.LoginAsync(request);

			if (!result.Success)
			{
				return Unauthorized(result);
			}

			_logger.LogInformation("Benutzer angemeldet: {Username}", request.Username);
			return Ok(result);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Fehler bei der Anmeldung");
			return StatusCode(500, new AuthResponse
			{
				Success = false,
				Message = "Ein Fehler ist aufgetreten"
			});
		}
	}

	/// <summary>
	/// Testet die Authentifizierung
	/// </summary>
	[HttpGet("test")]
	[Authorize]
	public IActionResult Test()
	{
		var username = User.Identity?.Name;
		return Ok(new { Message = $"Authentifizierung erfolgreich für {username}" });
	}
}
