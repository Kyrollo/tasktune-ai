using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using TaskTune.Application.DTOs.Auth;
using TaskTune.Application.Interfaces;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AccountSecurityWorkflowService _accountWorkflow;
    private readonly TaskTuneDbContext _db;

    public AuthController(
        IAuthService authService,
        AccountSecurityWorkflowService accountWorkflow,
        TaskTuneDbContext db)
    {
        _authService = authService;
        _accountWorkflow = accountWorkflow;
        _db = db;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null) return Unauthorized(new { message = "Invalid credentials." });
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    [EnableRateLimiting("auth")]
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

        var sessionClaim = User.FindFirst("sid")?.Value;
        if (!Guid.TryParse(sessionClaim, out var sessionId))
            return Unauthorized();

        await _authService.LogoutAsync(userId, sessionId);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto, CancellationToken cancellationToken)
    {
        await _accountWorkflow.RequestPasswordResetAsync(dto.Email, cancellationToken);
        return Ok(new { message = "If the email exists, a reset link will be sent." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        var result = await _accountWorkflow.ResetPasswordAsync(
            dto.Token, dto.Password, dto.ConfirmPassword, cancellationToken);
        if (!result.Success) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Password reset successfully. Sign in with your new password." });
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _accountWorkflow.CompleteInvitationAsync(
            dto.InviteCode ?? string.Empty,
            dto.Email,
            dto.FirstName,
            dto.LastName,
            dto.Password,
            dto.ConfirmPassword,
            cancellationToken);

        if (!result.Success) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Account created. Your invitation email has been verified; you can now sign in." });
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var currentSession = User.FindFirst("sid")?.Value;
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId.Value)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.SessionId,
                s.CreatedAt,
                s.ExpiresAt,
                s.RevokedAt,
                s.UserAgent,
                s.IpAddress,
                IsCurrent = s.SessionId.ToString() == currentSession,
                IsActive = s.RevokedAt == null && s.ConsumedAt == null && s.ExpiresAt > DateTime.UtcNow
            })
            .Take(50)
            .ToListAsync();
        return Ok(sessions);
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var session = await _db.UserSessions.FirstOrDefaultAsync(s =>
            s.SessionId == sessionId && s.UserId == userId.Value);
        if (session == null) return NotFound();

        var family = await _db.UserSessions.Where(s =>
            s.UserId == userId.Value && s.FamilyId == session.FamilyId && s.RevokedAt == null).ToListAsync();
        foreach (var item in family) item.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value;
        return int.TryParse(claim, out var userId) ? userId : null;
    }
}

public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Token, string Password, string ConfirmPassword);

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
