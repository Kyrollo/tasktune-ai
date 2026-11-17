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
        { 0, "Employee" }, { 1, "Supervisor" }, { 2, "Manager" },
        { 3, "HR" }, { 4, "CEO" }, { 5, "Admin" }
    };

    public AuthService(TaskTuneDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            return null;

        // Only approved password hashes are accepted. Legacy plaintext credentials must be reset.
        bool passwordValid;
        try
        {
            passwordValid = user.PasswordHash.StartsWith("$2", StringComparison.Ordinal) &&
                            BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            passwordValid = false;
        }
        catch (ArgumentException)
        {
            passwordValid = false;
        }

        if (!passwordValid)
            return null;

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenDays());
        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var roleName = RoleMap.GetValueOrDefault(user.Role, "Employee");
        return new TokenResponseDto(
            token,
            refreshToken,
            DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes()),
            new UserSummaryDto(
                user.UserId,
                user.FullName ?? $"{user.FirstName} {user.LastName}",
                user.Email,
                roleName));
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256)
            return null;

        var tokenHash = HashRefreshToken(refreshToken);
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.RefreshToken == tokenHash &&
                 u.RefreshTokenExpiry > DateTime.UtcNow &&
                 u.IsActive);

        if (user == null)
            return null;

        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();

        // Rotation: only the hash is persisted. The raw token is returned once to the client.
        user.RefreshToken = HashRefreshToken(newRefreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenDays());
        await _context.SaveChangesAsync();

        var roleName = RoleMap.GetValueOrDefault(user.Role, "Employee");
        return new TokenResponseDto(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes()),
            new UserSummaryDto(
                user.UserId,
                user.FullName ?? $"{user.FirstName} {user.LastName}",
                user.Email,
                roleName));
    }

    public async Task<bool> LogoutAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<object?> RegisterAsync(dynamic request)
    {
        // This endpoint can create a tenant owner only when explicitly enabled by deployment policy.
        // Joining existing tenants requires a validated invitation flow; client-supplied CompanyId is never trusted.
        if (!_configuration.GetValue<bool>("Registration:AllowCompanySelfService"))
            return null;

        string email = ((string)request.Email).Trim().ToLowerInvariant();
        string password = (string)request.Password;
        string firstName = ((string)request.FirstName).Trim();
        string lastName = ((string)request.LastName).Trim();
        int? requestedCompanyId = (int?)request.CompanyId;
        string? companyName = (string?)request.CompanyName;

        if (requestedCompanyId.HasValue || string.IsNullOrWhiteSpace(companyName) ||
            string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            password.Length < 12)
            return null;

        if (await _context.Users.AnyAsync(u => u.Email == email))
            return null;

        var company = new Company
        {
            CompanyName = companyName.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            Company = company,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            Role = 5,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Companies.Add(company);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new
        {
            message = "Company owner account created. Please sign in.",
            userId = user.UserId,
            email = user.Email
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = _configuration["JWT_SECRET"] ?? jwtSettings["SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
            throw new InvalidOperationException("JWT signing secret is not configured securely.");

        var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT issuer is not configured.");
        var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT audience is not configured.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var roleName = RoleMap.GetValueOrDefault(user.Role, "Employee");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("role", roleName),
            new("companyId", user.CompanyId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes()),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private int GetAccessTokenMinutes()
    {
        var value = _configuration.GetValue<int?>("JwtSettings:AccessTokenExpirationMinutes") ?? 15;
        return Math.Clamp(value, 5, 60);
    }

    private int GetRefreshTokenDays()
    {
        var value = _configuration.GetValue<int?>("JwtSettings:RefreshTokenExpirationDays") ?? 7;
        return Math.Clamp(value, 1, 30);
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashRefreshToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
