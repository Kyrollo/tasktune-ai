using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Middleware;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantMembershipMiddlewareTests
{
    [Fact]
    public async System.Threading.Tasks.Task SuspendedTenantRejectsRegularUserWithForbidden()
    {
        var (db, sid) = await CreateScenarioAsync("Suspended");
        await using (db)
        {
            var called = false;
            var context = CreateContext(sid, false);
            var middleware = new TenantMembershipMiddleware(_ => { called = true; return System.Threading.Tasks.Task.CompletedTask; });
            await middleware.InvokeAsync(context, db);
            Assert.False(called);
            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task PlatformAdminCanReachRecoveryRoutesForSuspendedTenant()
    {
        var (db, sid) = await CreateScenarioAsync("Suspended");
        await using (db)
        {
            var called = false;
            var context = CreateContext(sid, true);
            var middleware = new TenantMembershipMiddleware(_ => { called = true; return System.Threading.Tasks.Task.CompletedTask; });
            await middleware.InvokeAsync(context, db);
            Assert.True(called);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task ActiveTenantAllowsValidMember()
    {
        var (db, sid) = await CreateScenarioAsync("Active");
        await using (db)
        {
            var called = false;
            var context = CreateContext(sid, false);
            var middleware = new TenantMembershipMiddleware(_ => { called = true; return System.Threading.Tasks.Task.CompletedTask; });
            await middleware.InvokeAsync(context, db);
            Assert.True(called);
        }
    }

    private static DefaultHttpContext CreateContext(Guid sessionId, bool platformAdmin)
    {
        var claims = new List<Claim>
        {
            new("sub", "10"), new("companyId", "1"), new("sid", sessionId.ToString())
        };
        if (platformAdmin) claims.Add(new Claim("platform_admin", "true"));
        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "UnitTest")) };
    }

    private static async System.Threading.Tasks.Task<(TaskTuneDbContext Db, Guid SessionId)> CreateScenarioAsync(string status)
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company { CompanyId = 1, CompanyName = "Middleware Tenant", SettingsJson = "{}", Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var user = new User
        {
            UserId = 10, CompanyId = 1, Company = company, Email = "member@tenant.test",
            PasswordHash = "test-hash", FirstName = "Test", LastName = "Member", FullName = "Test Member",
            Role = 0, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var sid = Guid.NewGuid();
        db.Companies.Add(company);
        db.Users.Add(user);
        db.UserSessions.Add(new UserSession
        {
            SessionId = sid, UserId = 10, User = user, FamilyId = Guid.NewGuid(),
            TokenHash = new string('A', 64), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();
        return (db, sid);
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
