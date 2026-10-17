namespace TaskTune.Application.DTOs.Auth;

public record TokenResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserSummaryDto User
);

