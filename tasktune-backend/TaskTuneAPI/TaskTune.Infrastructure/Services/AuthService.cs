using Microsoft.EntityFrameworkCore;
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

    private static readonly Dictionary<byte, string> RoleMap = new()
    {
        { 0, "Employee" },
        { 1, "Supervisor" },
        { 2, "Manager" },
        { 3, "HR" },
        { 4, "CEO" },
        { 5, "Admin" }
    };

    public AuthService(TaskTuneDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.Email == request.Email && u.IsActive);

        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return null;
        }

        bool passwordValid;
        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            passwordValid = false;
        }

        if (!passwordValid)
        {
            return null;
        }

        var (accessToken, expiresAt) = GenerateJwtToken(user);
        var refreshToken = CreateRefreshToken();

        user.RefreshToken = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays());
        user.LastLogin = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new TokenResponseDto(
            accessToken,
            refreshToken,
            expiresAt,
            new UserSummaryDto(
                user.UserId,
                user.FullName ?? $"{user.FirstName} {user.LastName}",
                user.Email,
                RoleMap.GetValueOrDefault(user.Role, "Employee"))
        );
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256)
        {
            return null;
        }

        var refreshTokenHash = HashRefreshToken(refreshToken);
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.RefreshToken == refreshTokenHash
              && u.RefreshTokenExpiry > DateTime.UtcNow
              && u.IsActive);

        if (user == null)
        {
            return null;
        }

        var (accessToken, expiresAt) = GenerateJwtToken(user);
        var rotatedRefreshToken = CreateRefreshToken();

        user.RefreshToken = HashRefreshToken(rotatedRefreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays());
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new TokenResponseDto(
            accessToken,
            rotatedRefreshToken,
            expiresAt,
            new UserSummaryDto(
                user.UserId,
                user.FullName ?? $"{user.FirstName} {user.LastName}",
                user.Email,
                RoleMap.GetValueOrDefault(user.Role, "Employee"))
        );
    }

    public async Task<bool> LogoutAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetRequiredSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:SecretKey must contain at least 32 UTF-8 bytes.");
        }

        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(GetAccessTokenExpirationMinutes());

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
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
        return minutes > 0
            ? minutes
            : throw new InvalidOperationException("JwtSettings:AccessTokenExpirationMinutes must be positive.");
    }

    private int GetRefreshTokenExpirationDays()
    {
        var days = _configuration.GetValue<int?>("JwtSettings:RefreshTokenExpirationDays") ?? 7;
        return days > 0
            ? days
            : throw new InvalidOperationException("JwtSettings:RefreshTokenExpirationDays must be positive.");
    }

    private static string CreateRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
