using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TaskTune.Application.DTOs;
using TaskTune.Application.DTOs.Auth;
using TaskTune.Application.Interfaces;
using TaskTune.Infrastructure.Models;

namespace TaskTune.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly TaskTuneDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    private static readonly Dictionary<byte, string> RoleMap = new()
    {
        { 0, "Employee" },
        { 1, "Supervisor" },
        { 2, "Manager" },
        { 3, "HR" },
        { 4, "CEO" },
        { 5, "Admin" }
    };

    public AuthService(TaskTuneDbContext context, IConfiguration configuration, IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var normalizedEmail = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrEmpty(request.Password))
            return null;

        var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(
            u => u.Email == normalizedEmail && u.IsActive);
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            return null;

        bool passwordValid;
        try { passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash); }
        catch { passwordValid = false; }
        if (!passwordValid) return null;

        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var refreshToken = CreateRefreshToken();
        var expiresAt = now.AddDays(GetRefreshTokenExpirationDays());

        // Legacy single-refresh-token columns are deliberately cleared. All new sessions
        // are represented in UserSessions, with only a SHA-256 digest persisted.
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.LastLogin = now;
        user.UpdatedAt = now;
        _context.UserSessions.Add(new UserSession
        {
            SessionId = sessionId,
            UserId = user.UserId,
            FamilyId = familyId,
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            UserAgent = CurrentUserAgent,
            IpAddress = CurrentIpAddress
        });

        var (accessToken, accessExpiresAt) = GenerateJwtToken(user, sessionId);
        await _context.SaveChangesAsync();
        return BuildResponse(user, accessToken, refreshToken, accessExpiresAt);
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256)
            return null;

        var tokenHash = HashRefreshToken(refreshToken);
        var session = await _context.UserSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash);
        if (session == null)
            return null;

        if (session.ConsumedAt.HasValue)
        {
            // Reuse of an already-rotated token is a replay signal. Revoke every session
            // in the family, including the most recently issued replacement.
            await RevokeFamilyAsync(session.UserId, session.FamilyId);
            return null;
        }

        if (session.RevokedAt.HasValue || session.ExpiresAt <= DateTime.UtcNow)
            return null;

        var user = await _context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == session.UserId);
        if (user == null || !user.IsActive)
        {
            await RevokeFamilyAsync(session.UserId, session.FamilyId);
            return null;
        }

        var now = DateTime.UtcNow;
        var nextSessionId = Guid.NewGuid();
        var nextRefreshToken = CreateRefreshToken();
        var nextExpiry = now.AddDays(GetRefreshTokenExpirationDays());
        var nextSession = new UserSession
        {
            SessionId = nextSessionId,
            UserId = user.UserId,
            FamilyId = session.FamilyId,
            TokenHash = HashRefreshToken(nextRefreshToken),
            CreatedAt = now,
            ExpiresAt = nextExpiry,
            UserAgent = CurrentUserAgent,
            IpAddress = CurrentIpAddress
        };

        var relational = _context.Database.IsRelational();
        if (relational)
            await _context.Database.BeginTransactionAsync();

        session.ConsumedAt = now;
        session.ReplacedBySessionId = nextSessionId;
        user.UpdatedAt = now;
        _context.UserSessions.Add(nextSession);

        var (accessToken, accessExpiresAt) = GenerateJwtToken(user, nextSessionId);
        try
        {
            await _context.SaveChangesAsync();
            if (relational)
                await _context.Database.CommitTransactionAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (relational)
                await _context.Database.RollbackTransactionAsync();
            _context.ChangeTracker.Clear();
            await RevokeFamilyAsync(user.UserId, session.FamilyId);
            return null;
        }
        catch
        {
            if (relational)
                await _context.Database.RollbackTransactionAsync();
            throw;
        }

        return BuildResponse(user, accessToken, nextRefreshToken, accessExpiresAt);
    }

    public async Task<bool> LogoutAsync(int userId, Guid? sessionId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return false;

        var now = DateTime.UtcNow;
        var sessions = _context.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null);
        if (sessionId.HasValue)
        {
            var current = await _context.UserSessions.FirstOrDefaultAsync(s =>
                s.SessionId == sessionId.Value && s.UserId == userId);
            if (current == null) return true;
            sessions = _context.UserSessions.Where(s =>
                s.UserId == userId && s.FamilyId == current.FamilyId && s.RevokedAt == null);
        }

        var activeSessions = await sessions.ToListAsync();
        foreach (var session in activeSessions)
            session.RevokedAt = now;

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync();
        return true;
    }

    private async System.Threading.Tasks.Task RevokeFamilyAsync(int userId, Guid familyId)
    {
        var now = DateTime.UtcNow;
        var sessions = await _context.UserSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == userId && s.FamilyId == familyId && s.RevokedAt == null)
            .ToListAsync();
        foreach (var session in sessions)
            session.RevokedAt = now;
        await _context.SaveChangesAsync();
    }

    private string? CurrentUserAgent
    {
        get
        {
            var value = _httpContextAccessor?.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 500)];
        }
    }

    private string? CurrentIpAddress
    {
        get
        {
            var value = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 45)];
        }
    }

    private TokenResponseDto BuildResponse(User user, string accessToken, string refreshToken, DateTime expiresAt) =>
        new(
            accessToken,
            refreshToken,
            expiresAt,
            new UserSummaryDto(
                user.UserId,
                user.FullName ?? $"{user.FirstName} {user.LastName}",
                user.Email,
                RoleMap.GetValueOrDefault(user.Role, "Employee")));

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(User user, Guid sessionId)
    {
        var jwtSettings = _configuration.GetRequiredSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("JwtSettings:SecretKey must contain at least 32 UTF-8 bytes.");

        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(GetAccessTokenExpirationMinutes());

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", sessionId.ToString()),
            new("role", RoleMap.GetValueOrDefault(user.Role, "Employee")),
            new("companyId", user.CompanyId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private int GetAccessTokenExpirationMinutes()
    {
        var minutes = _configuration.GetValue<int?>("JwtSettings:AccessTokenExpirationMinutes") ?? 60;
        return minutes > 0 ? minutes
            : throw new InvalidOperationException("JwtSettings:AccessTokenExpirationMinutes must be positive.");
    }

    private int GetRefreshTokenExpirationDays()
    {
        var days = _configuration.GetValue<int?>("JwtSettings:RefreshTokenExpirationDays") ?? 7;
        return days > 0 ? days
            : throw new InvalidOperationException("JwtSettings:RefreshTokenExpirationDays must be positive.");
    }

    private static string CreateRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
