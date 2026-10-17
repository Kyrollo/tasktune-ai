using Microsoft.Ajax.Utilities;
using System;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;
using BCrypt.Net;

namespace TaskTuneAPI.Controllers
{

    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpPost, Route("login")]
        public async Task<IHttpActionResult> Login(LoginRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Email and password are required");

            var email = dto.Email.Trim();

            var user = await db.Users
                .Include(u => u.Company)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .FirstOrDefaultAsync(u => u.Email == email);

            var isValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

            if (!isValid)
                return Content(System.Net.HttpStatusCode.Unauthorized, new
                {
                    step = "password_mismatch",
                    email = user.Email,
                    hash = user.PasswordHash
                });

            var accessToken = JwtHelper.GenerateToken(user.UserId, user.Email, user.Role, user.CompanyId);
            var refreshToken = JwtHelper.GenerateRefreshToken();
            var refreshDays = int.Parse(ConfigurationManager.AppSettings["JwtRefreshExpirationDays"]);

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshDays);
            user.LastLogin = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var expMinutes = int.Parse(ConfigurationManager.AppSettings["JwtExpirationMinutes"]);

            return Ok(new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expMinutes),
                User = MapUserResponse(user)
            });
        }

        //[HttpPost, Route("rehash-all-users")]
        //public async Task<IHttpActionResult> RehashAllUsers()
        //{
        //    var users = await db.Users
        //        .Where(u => u.IsActive)
        //        .ToListAsync();

        //    foreach (var user in users)
        //    {
        //        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        //        user.RefreshToken = null;
        //        user.RefreshTokenExpiry = null;
        //    }

        //    await db.SaveChangesAsync();

        //    return Ok(new
        //    {
        //        message = "All active users were rehashed successfully.",
        //        count = users.Count
        //    });
        //}

        [HttpPost, Route("refresh-token")]
        public async Task<IHttpActionResult> RefreshToken(RefreshTokenRequestDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.RefreshToken))
                return BadRequest("Refresh token is required");

            var user = await db.Users
                .Include(u => u.Company)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken
                                        && u.RefreshTokenExpiry > DateTime.UtcNow
                                        && u.IsActive);

            if (user == null)
                return Unauthorized();

            var accessToken = JwtHelper.GenerateToken(user.UserId, user.Email, user.Role, user.CompanyId);
            var newRefreshToken = JwtHelper.GenerateRefreshToken();
            var refreshDays = int.Parse(ConfigurationManager.AppSettings["JwtRefreshExpirationDays"]);

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(refreshDays);
            await db.SaveChangesAsync();

            var expMinutes = int.Parse(ConfigurationManager.AppSettings["JwtExpirationMinutes"]);

            return Ok(new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expMinutes),
                User = MapUserResponse(user)
            });
        }

        [HttpPost, Route("logout")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Logout()
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var user = await db.Users.FindAsync(userId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                await db.SaveChangesAsync();
            }
            return Ok(new MessageResponseDto { Message = "Logged out successfully" });
        }

        [HttpPost, Route("forgot-password")]
        public async Task<IHttpActionResult> ForgotPassword(ForgotPasswordRequestDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Email))
                return BadRequest("Email is required");

            // Just acknowledge — don't reveal whether the email exists
            return Ok(new MessageResponseDto { Message = "If this email exists, a reset request has been sent to the admin." });
        }

        private UserResponseDto MapUserResponse(User user)
        {
            return new UserResponseDto
            {
                UserId = user.UserId,
                CompanyId = user.CompanyId,
                CompanyName = user.Company?.CompanyName,
                DepartmentId = user.DepartmentId,
                DepartmentName = user.Department?.DepartmentName,
                PositionId = user.PositionId,
                PositionTitle = user.Position?.PositionTitle,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Phone = user.Phone,
                Role = user.Role,
                RoleName = RoleNames.GetName(user.Role),
                IsActive = user.IsActive,
                LastLogin = user.LastLogin,
                CreatedAt = user.CreatedAt
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}