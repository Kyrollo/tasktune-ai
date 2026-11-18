using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class AccountSecurityWorkflowTests
{
    [Fact]
    public async System.Threading.Tasks.Task Invitation_IsCompanyScoped_EmailBound_AndSingleUse()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company
        {
            CompanyId = 1, CompanyName = "Invitation Tenant", SettingsJson = "{}",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var admin = CreateUser(99, 1, "admin@tenant.test", 5);
        var plan = CreateSeatPlan(1, 5);
        db.Companies.Add(company);
        db.Users.Add(admin);
        db.SubscriptionPlans.Add(plan);
        db.TenantSubscriptions.Add(CreateSubscription(1, plan));
        await db.SaveChangesAsync();

        var email = new FakeEmailSender();
        var service = new AccountSecurityWorkflowService(db, email, CreateConfiguration(), new TenantSeatCapacityService(db));

        var created = await service.CreateInvitationAsync(
            companyId: 1, actorId: 99, email: "new.employee@tenant.test", role: 0,
            departmentId: null, positionId: null);
        Assert.True(created.Success);
        Assert.NotNull(email.LastBody);
        var token = ExtractToken(email.LastBody!, "invite");

        var completed = await service.CompleteInvitationAsync(
            token, "new.employee@tenant.test", "New", "Employee",
            "Correct-Horse-Battery-42!", "Correct-Horse-Battery-42!");
        Assert.True(completed.Success);
        Assert.NotNull(completed.UserId);

        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "new.employee@tenant.test");
        Assert.Equal(1, user.CompanyId);
        Assert.Equal((byte)0, user.Role);
        Assert.True(BCrypt.Net.BCrypt.Verify("Correct-Horse-Battery-42!", user.PasswordHash));

        var replay = await service.CompleteInvitationAsync(
            token, "new.employee@tenant.test", "New", "Employee",
            "Correct-Horse-Battery-42!", "Correct-Horse-Battery-42!");
        Assert.False(replay.Success);
    }

    [Fact]
    public async System.Threading.Tasks.Task InvitationCountsActiveUsersAndPendingInvitationsAgainstSeatLimit()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company { CompanyId = 1, CompanyName = "Seat Tenant", SettingsJson = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var admin = CreateUser(99, 1, "admin@tenant.test", 5);
        var plan = CreateSeatPlan(1, 2);
        db.Companies.Add(company);
        db.Users.Add(admin);
        db.SubscriptionPlans.Add(plan);
        db.TenantSubscriptions.Add(CreateSubscription(1, plan));
        db.AccountSecurityTokens.Add(new AccountSecurityToken
        {
            CompanyId = 1, Email = "pending@tenant.test", Purpose = "Invitation",
            TokenHash = new string('B', 64), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        var email = new FakeEmailSender();
        var service = new AccountSecurityWorkflowService(db, email, CreateConfiguration(), new TenantSeatCapacityService(db));
        var result = await service.CreateInvitationAsync(1, admin.UserId, "next@tenant.test", 0, null, null);

        Assert.False(result.Success);
        Assert.Contains("seat limit", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(email.LastBody);
        Assert.Equal(1, await db.AccountSecurityTokens.CountAsync(token =>
            token.Purpose == "Invitation" && token.ConsumedAt == null && token.ExpiresAt > DateTime.UtcNow));
    }

    [Fact]
    public async System.Threading.Tasks.Task PasswordReset_IsSingleUse_AndRevokesAllRefreshSessions()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company
        {
            CompanyId = 1, CompanyName = "Reset Tenant", SettingsJson = "{}",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var user = CreateUser(7, 1, "employee@tenant.test", 0);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Old-Password-Value-2026!");
        db.Companies.Add(company);
        db.Users.Add(user);
        db.UserSessions.Add(new UserSession
        {
            SessionId = Guid.NewGuid(),
            UserId = user.UserId,
            User = user,
            FamilyId = Guid.NewGuid(),
            TokenHash = new string('A', 64),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var email = new FakeEmailSender();
        var service = new AccountSecurityWorkflowService(db, email, CreateConfiguration(), new TenantSeatCapacityService(db));
        await service.RequestPasswordResetAsync("EMPLOYEE@TENANT.TEST");
        Assert.NotNull(email.LastBody);
        var token = ExtractToken(email.LastBody!, "token");

        var reset = await service.ResetPasswordAsync(
            token, "New-Password-Value-2026!", "New-Password-Value-2026!");
        Assert.True(reset.Success);

        var updatedUser = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.UserId == 7);
        Assert.True(BCrypt.Net.BCrypt.Verify("New-Password-Value-2026!", updatedUser.PasswordHash));
        var session = await db.UserSessions.IgnoreQueryFilters().SingleAsync();
        Assert.NotNull(session.RevokedAt);

        var replay = await service.ResetPasswordAsync(
            token, "Another-Password-Value-2026!", "Another-Password-Value-2026!");
        Assert.False(replay.Success);
        Assert.False(BCrypt.Net.BCrypt.Verify("Another-Password-Value-2026!", updatedUser.PasswordHash));
    }

    private static SubscriptionPlan CreateSeatPlan(int id, int maxUsers) => new()
    {
        PlanId = id, Code = $"seat-{id}", Name = "Seat plan", IsActive = true,
        MaxUsers = maxUsers, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static TenantSubscription CreateSubscription(int companyId, SubscriptionPlan plan) => new()
    {
        CompanyId = companyId, PlanId = plan.PlanId, Plan = plan, Status = "Active",
        StartsAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, int companyId, string email, byte role) => new()
    {
        UserId = id,
        CompanyId = companyId,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin-Password-Value-2026!"),
        FirstName = "Test",
        LastName = "User",
        FullName = "Test User",
        Role = role,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static string ExtractToken(string body, string queryKey)
    {
        var match = Regex.Match(body, $@"[?&]{Regex.Escape(queryKey)}=([A-Za-z0-9_-]+)");
        Assert.True(match.Success, $"The {queryKey} token was not present in the test email.");
        return match.Groups[1].Value;
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Web:BaseUrl"] = "https://app.tasktune.test"
            })
            .Build();

    private sealed class FakeEmailSender : ITransactionalEmailSender
    {
        public string? LastRecipient { get; private set; }
        public string? LastBody { get; private set; }

        public System.Threading.Tasks.Task SendAsync(
            string recipient, string subject, string body, CancellationToken cancellationToken = default)
        {
            LastRecipient = recipient;
            LastBody = body;
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
