using TaskTune.Application.DTOs.Auth;

namespace TaskTune.Application.Interfaces;

public interface IAuthService
{
    Task<TokenResponseDto?> LoginAsync(LoginRequestDto request, string? userAgent = null, string? ipAddress = null);
    Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken, string? userAgent = null, string? ipAddress = null);
    Task<bool> LogoutAsync(int userId, Guid? sessionId);
}
