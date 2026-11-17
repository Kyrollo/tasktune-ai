using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Middleware;

public sealed class TenantMembershipMiddleware
{
    private readonly RequestDelegate _next;
    public TenantMembershipMiddleware(RequestDelegate next) => _next = next;

    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, TaskTuneDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var company = context.User.FindFirst("companyId")?.Value;
            var sessionClaim = context.User.FindFirst("sid")?.Value;
            if (!int.TryParse(sub, out var userId) || !int.TryParse(company, out var companyId)
                || !Guid.TryParse(sessionClaim, out var sessionId)
                || userId <= 0 || companyId <= 0
                || !await db.Users.AnyAsync(u => u.UserId == userId && u.CompanyId == companyId && u.IsActive, context.RequestAborted)
                || !await db.UserSessions.AnyAsync(s =>
                    s.SessionId == sessionId && s.UserId == userId &&
                    s.RevokedAt == null && s.ConsumedAt == null && s.ExpiresAt > DateTime.UtcNow,
                    context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { message = "The authenticated session is inactive, revoked, expired, or no longer belongs to this company." },
                    context.RequestAborted);
                return;
            }
        }
        await _next(context);
    }
}
