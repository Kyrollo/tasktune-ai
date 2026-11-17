using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class CommunicationApprovalWorkflowTests
{
    [Fact]
    public async System.Threading.Tasks.Task RequesterCannotApprove_AnotherAdminCanQueueDelivery_AndDecisionIsSingleUse()
    {
        await using var db = CreateContext();
        var seeded = await SeedPendingApprovalAsync(db);
        var service = new CommunicationApprovalWorkflowService(db);

        var selfApproval = await service.ApproveAsync(
            seeded.Company.CompanyId, seeded.Approval.ApprovalId,
            seeded.Requester.UserId, "Trying to approve my own request");
        Assert.False(selfApproval.Success);
        Assert.Equal("PendingApproval", seeded.Approval.Status);
        Assert.Equal("PendingApproval", seeded.Outbox.Status);

        var approved = await service.ApproveAsync(
            seeded.Company.CompanyId, seeded.Approval.ApprovalId,
            seeded.Reviewer.UserId, "Approved after review");
        Assert.True(approved.Success);
        Assert.Equal(1, approved.QueuedCount);
        Assert.Equal("Approved", seeded.Approval.Status);
        Assert.Equal(seeded.Reviewer.UserId, seeded.Approval.ReviewedByUserId);
        Assert.Equal("Approved after review", seeded.Approval.ReviewNote);
        Assert.Equal("Queued", seeded.Outbox.Status);

        var secondDecision = await service.RejectAsync(
            seeded.Company.CompanyId, seeded.Approval.ApprovalId,
            seeded.Reviewer.UserId, "Changing my mind");
        Assert.False(secondDecision.Success);
        Assert.Equal("Queued", seeded.Outbox.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task RejectionStopsEveryPendingOutboxRecipient()
    {
        await using var db = CreateContext();
        var seeded = await SeedPendingApprovalAsync(db);
        var service = new CommunicationApprovalWorkflowService(db);

        var rejected = await service.RejectAsync(
            seeded.Company.CompanyId, seeded.Approval.ApprovalId,
            seeded.Reviewer.UserId, "Policy review failed");

        Assert.True(rejected.Success);
        Assert.Equal("Rejected", seeded.Approval.Status);
        Assert.Equal("Rejected", seeded.Outbox.Status);
        Assert.Equal(seeded.Reviewer.UserId, seeded.Approval.ReviewedByUserId);
    }

    private static TaskTuneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TaskTuneDbContext(options, new FixedTenantContext(42));
    }

    private static async System.Threading.Tasks.Task<SeededApproval> SeedPendingApprovalAsync(TaskTuneDbContext db)
    {
        var now = DateTime.UtcNow;
        var company = new Company
        {
            CompanyId = 42,
            CompanyName = "Approval Tenant",
            SettingsJson = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };
        var requester = CreateUser(100, 42, "requester@approval.test", "Requester", "One", 3);
        var reviewer = CreateUser(101, 42, "reviewer@approval.test", "Reviewer", "Two", 5);
        var recipient = CreateUser(102, 42, "recipient@approval.test", "Recipient", "Three", 0);
        db.Companies.Add(company);
        db.Users.AddRange(requester, reviewer, recipient);
        await db.SaveChangesAsync();

        var communication = new CommunicationLog
        {
            CompanyId = company.CompanyId,
            Company = company,
            SentBy = requester.UserId,
            SentByNavigation = requester,
            RecipientUserId = recipient.UserId,
            RecipientUser = recipient,
            CommunicationType = 1,
            Subject = "Approval test",
            Body = "This message must be approved before delivery.",
            SentAt = now
        };
        db.CommunicationLogs.Add(communication);

        var approval = new CommunicationApproval
        {
            BatchId = Guid.NewGuid(),
            CompanyId = company.CompanyId,
            Company = company,
            RequestedByUserId = requester.UserId,
            RequestedBy = requester,
            Status = "PendingApproval",
            RequestedAt = now
        };
        db.CommunicationApprovals.Add(approval);
        await db.SaveChangesAsync();

        var outbox = new CommunicationOutbox
        {
            CompanyId = company.CompanyId,
            Company = company,
            ApprovalId = approval.ApprovalId,
            Approval = approval,
            CommunicationId = communication.CommunicationId,
            Communication = communication,
            RecipientUserId = recipient.UserId,
            RecipientUser = recipient,
            RecipientEmail = recipient.Email,
            Subject = communication.Subject,
            Body = communication.Body,
            Status = "PendingApproval",
            AttemptCount = 0,
            CreatedAt = now,
            AvailableAt = now,
            IdempotencyKey = Guid.NewGuid()
        };
        db.CommunicationOutbox.Add(outbox);
        await db.SaveChangesAsync();

        return new SeededApproval(company, requester, reviewer, recipient, approval, outbox);
    }

    private static User CreateUser(int id, int companyId, string email, string first, string last, byte role) => new()
    {
        UserId = id,
        CompanyId = companyId,
        Email = email,
        PasswordHash = "test-hash",
        FirstName = first,
        LastName = last,
        FullName = $"{first} {last}",
        Role = role,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed record SeededApproval(
        Company Company,
        User Requester,
        User Reviewer,
        User Recipient,
        CommunicationApproval Approval,
        CommunicationOutbox Outbox);

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
