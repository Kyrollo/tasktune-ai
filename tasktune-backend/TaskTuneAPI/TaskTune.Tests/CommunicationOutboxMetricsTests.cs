using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class CommunicationOutboxMetricsTests
{
    [Fact]
    public async System.Threading.Tasks.Task Metrics_AreTenantScoped_AndExposeOldestQueueAge()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var one = CreateCompany(1);
        var two = CreateCompany(2);
        var userOne = CreateUser(10, one);
        var userTwo = CreateUser(20, two);
        db.Companies.AddRange(one, two);
        db.Users.AddRange(userOne, userTwo);
        var approvalOne = CreateApproval(101, one, userOne);
        var approvalTwo = CreateApproval(102, two, userTwo);
        db.CommunicationApprovals.AddRange(approvalOne, approvalTwo);
        await db.SaveChangesAsync();

        db.CommunicationOutbox.AddRange(
            CreateOutbox(1, one, approvalOne, "Pending", DateTime.UtcNow.AddMinutes(-20)),
            CreateOutbox(2, one, approvalOne, "Processing", DateTime.UtcNow.AddMinutes(-4)),
            CreateOutbox(3, one, approvalOne, "DeadLetter", DateTime.UtcNow.AddMinutes(-30)),
            CreateOutbox(4, two, approvalTwo, "Pending", DateTime.UtcNow.AddHours(-4)));
        await db.SaveChangesAsync();

        var metrics = await new CommunicationOutboxMetricsService(db).GetAsync(1, 15, true);
        Assert.Equal(1, metrics.Pending);
        Assert.Equal(1, metrics.Processing);
        Assert.Equal(1, metrics.DeadLetter);
        Assert.Equal(0, metrics.Sent);
        Assert.NotNull(metrics.OldestPendingAtUtc);
        Assert.True(metrics.OldestPendingAgeSeconds >= 15 * 60);
        Assert.True(metrics.QueueAgeWarning);
        Assert.True(metrics.DeliveryWorkerEnabled);
    }

    [Fact]
    public async System.Threading.Tasks.Task Metrics_DoNotWarnWhenDeliveryIsDisabled()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = CreateCompany(1);
        var user = CreateUser(10, company);
        db.Companies.Add(company);
        db.Users.Add(user);
        var approval = CreateApproval(101, company, user);
        db.CommunicationApprovals.Add(approval);
        await db.SaveChangesAsync();
        db.CommunicationOutbox.Add(CreateOutbox(1, company, approval, "Pending", DateTime.UtcNow.AddHours(-2)));
        await db.SaveChangesAsync();

        var metrics = await new CommunicationOutboxMetricsService(db).GetAsync(1, 15, false);
        Assert.False(metrics.QueueAgeWarning);
        Assert.False(metrics.DeliveryWorkerEnabled);
        Assert.Equal(1, metrics.Pending);
    }

    private static Company CreateCompany(int id) => new()
    {
        CompanyId = id, CompanyName = $"Metrics tenant {id}", SettingsJson = "{}",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
    private static User CreateUser(int id, Company company) => new()
    {
        UserId = id, CompanyId = company.CompanyId, Company = company,
        Email = $"user{id}@tenant.test", PasswordHash = "test-hash",
        FirstName = "Test", LastName = "User", FullName = "Test User",
        Role = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
    private static CommunicationApproval CreateApproval(int id, Company company, User user) => new()
    {
        ApprovalId = id, CompanyId = company.CompanyId, Company = company,
        RequestedByUserId = user.UserId, RequestedByUser = user,
        CommunicationType = 1, RecipientType = "Individual",
        RecipientEmailsJson = System.Text.Json.JsonSerializer.Serialize(new[] { "person@tenant.test" }),
        CcEmailsJson = "[]", Subject = "Metrics test", Body = "Test",
        Status = "Approved", IdempotencyKey = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
    };
    private static CommunicationOutbox CreateOutbox(long id, Company company, CommunicationApproval approval, string status, DateTime createdAt) => new()
    {
        OutboxId = id, CompanyId = company.CompanyId, ApprovalId = approval.ApprovalId, Approval = approval,
        RecipientEmail = $"recipient{id}@tenant.test", CcEmailsJson = "[]",
        Subject = "Metrics test", Body = "Test", Status = status,
        Attempts = status == "DeadLetter" ? 5 : 0, NextAttemptAt = DateTime.UtcNow,
        CreatedAt = createdAt, IdempotencyKey = id.ToString().PadLeft(64, 'A')
    };
    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
