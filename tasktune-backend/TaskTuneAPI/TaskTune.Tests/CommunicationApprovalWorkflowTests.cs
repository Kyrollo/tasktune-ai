using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class CommunicationApprovalWorkflowTests
{
    [Fact]
    public async System.Threading.Tasks.Task Approval_RequiresDifferentReviewer_AndCreatesDeterministicOutboxRows()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company { CompanyId = 1, CompanyName = "Approval Tenant", SettingsJson = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var requester = CreateUser(10, 1, "requester@tenant.test");
        var reviewer = CreateUser(11, 1, "reviewer@tenant.test");
        db.Companies.Add(company);
        db.Users.AddRange(requester, reviewer);
        var approval = CreateApproval(1, requester, ["one@tenant.test", "two@tenant.test"]);
        db.CommunicationApprovals.Add(approval);
        await db.SaveChangesAsync();

        var service = new CommunicationApprovalService(db);
        Assert.Equal(CommunicationApprovalResult.CannotReviewOwnRequest,
            await service.ApproveAsync(approval.ApprovalId, 1, requester.UserId));
        Assert.Equal(CommunicationApprovalResult.Success,
            await service.ApproveAsync(approval.ApprovalId, 1, reviewer.UserId));

        var stored = await db.CommunicationApprovals.SingleAsync();
        Assert.Equal("Approved", stored.Status);
        Assert.Equal(reviewer.UserId, stored.ReviewedByUserId);
        var outbox = await db.CommunicationOutbox.OrderBy(x => x.RecipientEmail).ToListAsync();
        Assert.Equal(2, outbox.Count);
        Assert.All(outbox, item => Assert.Equal("Pending", item.Status));
        Assert.Equal(2, outbox.Select(x => x.IdempotencyKey).Distinct().Count());
        Assert.All(outbox, item => Assert.Equal(64, item.IdempotencyKey.Length));
        Assert.Equal(CommunicationApprovalResult.AlreadyReviewed,
            await service.ApproveAsync(approval.ApprovalId, 1, reviewer.UserId));
        Assert.Equal(2, await db.CommunicationOutbox.CountAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task Approval_IsCompanyScoped()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(2));
        var service = new CommunicationApprovalService(db);
        Assert.Equal(CommunicationApprovalResult.NotFound, await service.ApproveAsync(123, 2, 20));
    }

    [Fact]
    public async System.Threading.Tasks.Task Rejection_RequiresAnotherReviewer_AndPersistsReason()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company { CompanyId = 1, CompanyName = "Reject Tenant", SettingsJson = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var requester = CreateUser(30, 1, "author@tenant.test");
        var reviewer = CreateUser(31, 1, "reviewer@tenant.test");
        db.Companies.Add(company);
        db.Users.AddRange(requester, reviewer);
        var approval = CreateApproval(2, requester, ["person@tenant.test"]);
        db.CommunicationApprovals.Add(approval);
        await db.SaveChangesAsync();

        var service = new CommunicationApprovalService(db);
        Assert.Equal(CommunicationApprovalResult.CannotReviewOwnRequest,
            await service.RejectAsync(approval.ApprovalId, 1, requester.UserId, "Not appropriate"));
        Assert.Equal(CommunicationApprovalResult.Success,
            await service.RejectAsync(approval.ApprovalId, 1, reviewer.UserId, "Policy violation"));

        var saved = await db.CommunicationApprovals.SingleAsync();
        Assert.Equal("Rejected", saved.Status);
        Assert.Equal("Policy violation", saved.RejectionReason);
        Assert.Empty(await db.CommunicationOutbox.ToListAsync());
    }

    private static CommunicationApproval CreateApproval(int id, User requester, List<string> recipients) => new()
    {
        ApprovalId = id, CompanyId = requester.CompanyId, RequestedByUserId = requester.UserId,
        RequestedByUser = requester, CommunicationType = 1, RecipientType = "Multiple Employees",
        RecipientEmailsJson = JsonSerializer.Serialize(recipients), CcEmailsJson = "[]",
        Subject = "Review notice", Body = "Please review this communication.",
        Status = "Pending", IdempotencyKey = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, int companyId, string email) => new()
    {
        UserId = id, CompanyId = companyId, Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test-Password-Value-2026!"),
        FirstName = "Test", LastName = "User", FullName = "Test User",
        Role = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
