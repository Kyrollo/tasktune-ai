using System;

namespace TaskTuneAPI.Models.DTOs
{
    public class LoginRequestDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class TokenResponseDto
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public UserResponseDto User { get; set; }
    }

    public class RefreshTokenRequestDto
    {
        public string RefreshToken { get; set; }
    }

    public class ForgotPasswordRequestDto
    {
        public string Email { get; set; }
    }

    public class MessageResponseDto
    {
        public string Message { get; set; }
    }
}