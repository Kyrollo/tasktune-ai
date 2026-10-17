using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);
        if (user == null)
            return null;

        // Support both BCrypt hashed and plain-text passwords
        bool passwordValid = false;
        if (user.PasswordHash.StartsWith("$2"))
        {
            try { passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash); }
            catch { passwordValid = false; }
        }
        else
        {
            passwordValid = user.PasswordHash == request.Password;
        }

        if (!passwordValid)
            return null;

        var token = GenerateJwtToken(user);
        var refreshToken = Guid.NewGuid().ToString("N");

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var roleName = RoleMap.GetValueOrDefault(user.Role, "Employee");

        return new TokenResponseDto(
            token,
            refreshToken,
            DateTime.UtcNow.AddHours(1),
            new UserSummaryDto(
                user.UserId,
                user.FullName ?? $"{user.FirstName} {user.LastName}",
                user.Email,
                roleName)
        );
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.RefreshToken == refreshToken
              && u.RefreshTokenExpiry > DateTime.UtcNow
              && u.IsActive);

        if (user == null)
            return null;

        var newToken = GenerateJwtToken(user);
        var newRefreshToken = Guid.NewGuid().ToString("N");

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        var roleName = RoleMap.GetValueOrDefault(user.Role, "Employee");

        return new TokenResponseDto(
            newToken,
            newRefreshToken,
            DateTime.UtcNow.AddHours(1),
            new UserSummaryDto(
                user.UserId,
                user.FullName ?? $"{user.FirstName} {user.LastName}",
                user.Email,
                roleName)
        );
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
        string email = (string)request.Email;
        string password = (string)request.Password;
        string firstName = (string)request.FirstName;
        string lastName = (string)request.LastName;
        int? companyId = (int?)request.CompanyId;
        string? companyName = (string?)request.CompanyName;

        if (await _context.Users.AnyAsync(u => u.Email == email))
            return null;

        if (companyId == null && !string.IsNullOrEmpty(companyName))
        {
            var company = new Company
            {
                CompanyName = companyName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();
            companyId = company.CompanyId;
        }

        if (companyId == null)
            companyId = 1;

        var user = new User
        {
            CompanyId = companyId.Value,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            FullName = $"{firstName} {lastName}".Trim(),
            Role = companyName != null ? (byte)5 : (byte)0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new
        {
            message = "Account created successfully.",
            userId = user.UserId,
            email = user.Email
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var roleName = RoleMap.GetValueOrDefault(user.Role, "Employee");

        // Use short claim names that match what the validator expects
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("role", roleName),
            new Claim("companyId", user.CompanyId.ToString())
        };

        var expirationMinutes = int.TryParse(jwtSettings["AccessTokenExpirationMinutes"], out var mins) ? mins : 60;

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}