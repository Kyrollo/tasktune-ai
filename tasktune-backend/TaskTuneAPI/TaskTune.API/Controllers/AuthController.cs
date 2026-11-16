using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskTune.Application.DTOs.Auth;
using TaskTune.Application.Interfaces;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null) return Unauthorized(new { message = "Invalid credentials." });
        return Ok(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
    {
        var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
        if (result == null) return Unauthorized(new { message = "Invalid or expired refresh token. Sign in again." });
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

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        // Fail closed until an email verification and one-time reset-token workflow is configured.
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            message = "Password reset is not configured. Contact your company administrator."
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        if (!_configuration.GetValue<bool>("Registration:AllowCompanySelfService"))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Public company registration is disabled. Ask your platform administrator for an invitation."
            });

        if (string.IsNullOrWhiteSpace(dto.Email) ||
            !new EmailAddressAttribute().IsValid(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password) ||
            dto.Password.Length < 12 ||
            string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.CompanyName) ||
            dto.CompanyId.HasValue)
            return BadRequest(new { message = "Provide a valid email, a password of at least 12 characters, your name, and a company name. Company ID cannot be supplied." });

        if (dto.Password != dto.ConfirmPassword)
            return BadRequest(new { message = "Passwords do not match." });

        var result = await _authService.RegisterAsync(dto);
        if (result == null)
            return Conflict(new { message = "Registration could not be completed. Verify the details or contact support." });

        return Ok(result);
    }
}

public record ForgotPasswordDto([Required, EmailAddress] string Email);

public record RegisterRequestDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(12)] string Password,
    [Required] string ConfirmPassword,
    [Required] string FirstName,
    [Required] string LastName,
    int? CompanyId,
    string? CompanyName,
    string? InviteCode
);
