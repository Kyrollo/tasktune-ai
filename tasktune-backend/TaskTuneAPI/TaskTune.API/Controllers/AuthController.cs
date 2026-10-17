using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using TaskTune.Application.DTOs.Auth;
using TaskTune.Application.Interfaces;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null) return Unauthorized(new { message = "Invalid credentials." });
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        if (result == null) return Unauthorized(new { message = "Invalid or expired refresh token." });
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                 ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(claim, out int userId))
            return Unauthorized();

        await _authService.LogoutAsync(userId);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        return Ok(new { message = "If the email exists, a reset link will be sent." });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "Email and password are required." });

            if (dto.Password != dto.ConfirmPassword)
                return BadRequest(new { message = "Passwords do not match." });

            var result = await _authService.RegisterAsync(dto);
            if (result == null)
                return Conflict(new { message = "Email already exists." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Registration failed.", error = ex.Message, innerError = ex.InnerException?.Message });
        }
    }
}

public record ForgotPasswordDto(string Email);

public record RegisterRequestDto(
    string Email,
    string Password,
    string ConfirmPassword,
    string FirstName,
    string LastName,
    int? CompanyId,
    string? CompanyName,
    string? InviteCode
);