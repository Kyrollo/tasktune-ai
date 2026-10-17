using TaskTune.Application.DTOs.Auth;

namespace TaskTune.Application.Interfaces;

public interface IAuthService
{
    Task<TokenResponseDto?> LoginAsync(LoginRequestDto request);
    Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(int userId);
    Task<object?> RegisterAsync(dynamic request);
}

