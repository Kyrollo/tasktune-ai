using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class CommunicationOutboxOperationsTests
{
    [Fact]
    public async System.Threading.Tasks.Task Replay_ResetsDeadLetterAndAuditsReconciliation_WithoutChangingIdempotencyKey()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = CreateCompany(1);
        var requester = CreateUser(10, company, "requester@tenant.test");
        var operatorUser = CreateUser(11, company, "operator@tenant.test");
        var approval = new CommunicationApproval
        {
            ApprovalId = 50,
            CompanyId = 1,
            Company = company,
            RequestedByUserId = requester.UserId,
            RequestedByUser = requester,
            CommunicationType = 1,
            RecipientType = "Individual",
            RecipientEmailsJson = "[\"recipient@tenant.test\"]",
            CcEmailsJson = "[]",
            Subject = "Policy update",
            Body = "Please review the updated policy.",
            Status = "Approved",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ReviewedByUserId = operatorUser.UserId,
            ReviewedByUser = operatorUser,
            ReviewedAt = DateTime.UtcNow
        };
        var originalIdempotencyKey = new string('A', 64);
        var outbox = new CommunicationOutbox
        {
            OutboxId = 100,
            CompanyId = 1,
            ApprovalId = approval.ApprovalId,
            Approval = approval,
            RecipientEmail = "recipient@tenant.test",
            CcEmailsJson = "[]",
            Subject = approval.Subject,
            Body = approval.Body,
            Status = "DeadLetter",
            Attempts = 5,
            NextAttemptAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LastError = "Provider acceptance is uncertain.",
            IdempotencyKey = originalIdempotencyKey,
            LeaseToken = Guid.NewGuid(),
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        db.Companies.Add(company);
        db.Users.AddRange(requester, operatorUser);
        db.CommunicationApprovals.Add(approval);
        db.CommunicationOutbox.Add(outbox);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "TaskTune.Tests";
        var service = new CommunicationOutboxOperationsService(db);
        var result = await service.ReplayDeadLetterAsync(
            outbox.OutboxId, 1, operatorUser.UserId, "Provider logs confirm no accepted message.", true, httpContext);

        Assert.Equal(CommunicationOutboxReplayResult.Success, result);
        Assert.Equal("Pending", outbox.Status);
        Assert.Equal(0, outbox.Attempts);
        Assert.Null(outbox.LeaseToken);
        Assert.Null(outbox.LeaseExpiresAt);
        Assert.Null(outbox.LastError);
        Assert.Equal(originalIdempotencyKey, outbox.IdempotencyKey);
        Assert.True(outbox.NextAttemptAt >= outbox.CreatedAt);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(operatorUser.UserId, audit.UserId);
        Assert.Equal("communication.outbox.replay", audit.Action);
        Assert.Equal("CommunicationOutbox", audit.ResourceType);
        Assert.Contains("Provider logs confirm no accepted message.", audit.ChangesJson);
        Assert.Contains(originalIdempotencyKey, audit.ChangesJson);

        var repeated = await service.ReplayDeadLetterAsync(
            outbox.OutboxId, 1, operatorUser.UserId, "Tried a second time after refresh.", true, httpContext);
        Assert.Equal(CommunicationOutboxReplayResult.NotDeadLetter, repeated);
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task Replay_RequiresExplicitProviderNonAcceptanceConfirmation()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = CreateCompany(1);
        var requester = CreateUser(10, company, "requester@tenant.test");
        var operatorUser = CreateUser(11, company, "operator@tenant.test");
        var approval = new CommunicationApproval
        {
            ApprovalId = 61, CompanyId = 1, Company = company,
            RequestedByUserId = requester.UserId, RequestedByUser = requester,
            RecipientType = "Individual",
            RecipientEmailsJson = System.Text.Json.JsonSerializer.Serialize(new[] { "recipient@tenant.test" }),
            CcEmailsJson = "[]", Subject = "Confirm provider state", Body = "Test",
            Status = "Approved", IdempotencyKey = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
        };
        var outbox = new CommunicationOutbox
        {
            OutboxId = 101, CompanyId = 1, ApprovalId = approval.ApprovalId, Approval = approval,
            RecipientEmail = "recipient@tenant.test", Subject = approval.Subject, Body = approval.Body,
            Status = "DeadLetter", Attempts = 5, CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow, IdempotencyKey = new string('D', 64)
        };
        db.Companies.Add(company);
        db.Users.AddRange(requester, operatorUser);
        db.CommunicationApprovals.Add(approval);
        db.CommunicationOutbox.Add(outbox);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var service = new CommunicationOutboxOperationsService(db);
        var result = await service.ReplayDeadLetterAsync(
            outbox.OutboxId, 1, operatorUser.UserId,
            "Operator has not verified provider acceptance outcome.", false, httpContext);

        Assert.Equal(CommunicationOutboxReplayResult.ProviderConfirmationRequired, result);
        Assert.Equal("DeadLetter", outbox.Status);
        Assert.Equal(5, outbox.Attempts);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task Replay_IsTenantScopedAndRejectsNonApprovedParent()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var companyOne = CreateCompany(1);
        var companyTwo = CreateCompany(2);
        var ownerTwo = CreateUser(20, companyTwo, "owner@two.test");
        var approvalTwo = new CommunicationApproval
        {
            ApprovalId = 70,
            CompanyId = 2,
            Company = companyTwo,
            RequestedByUserId = ownerTwo.UserId,
            RequestedByUser = ownerTwo,
            RecipientType = "Individual",
            RecipientEmailsJson = "[\"recipient@two.test\"]",
            CcEmailsJson = "[]",
            Subject = "Tenant two",
            Body = "Private content",
            Status = "Pending",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        var foreignOutbox = new CommunicationOutbox
        {
            OutboxId = 200,
            CompanyId = 2,
            ApprovalId = approvalTwo.ApprovalId,
            Approval = approvalTwo,
            RecipientEmail = "recipient@two.test",
            Subject = "Tenant two",
            Body = "Private content",
            Status = "DeadLetter",
            Attempts = 5,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow,
            IdempotencyKey = new string('B', 64)
        };
        db.Companies.AddRange(companyOne, companyTwo);
        db.Users.Add(ownerTwo);
        db.CommunicationApprovals.Add(approvalTwo);
        db.CommunicationOutbox.Add(foreignOutbox);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var service = new CommunicationOutboxOperationsService(db);
        var foreignResult = await service.ReplayDeadLetterAsync(
            foreignOutbox.OutboxId, 1, 20, "Attempt to replay another tenant's outbox.", true, httpContext);
        Assert.Equal(CommunicationOutboxReplayResult.NotFound, foreignResult);
        Assert.Equal("DeadLetter", foreignOutbox.Status);

        // The same item may only be replayed after its parent request has remained approved.
        var pendingApproval = new CommunicationApproval
        {
            ApprovalId = 71,
            CompanyId = 1,
            Company = companyOne,
            RequestedByUserId = 21,
            RequestedByUser = CreateUser(21, companyOne, "owner@one.test"),
            RecipientType = "Individual",
            RecipientEmailsJson = "[\"person@one.test\"]",
            CcEmailsJson = "[]",
            Subject = "No longer approved",
            Body = "Do not send",
            Status = "Rejected",
            IdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(pendingApproval.RequestedByUser);
        var rejectedOutbox = new CommunicationOutbox
        {
            OutboxId = 201,
            CompanyId = 1,
            ApprovalId = pendingApproval.ApprovalId,
            Approval = pendingApproval,
            RecipientEmail = "person@one.test",
            Subject = "No longer approved",
            Body = "Do not send",
            Status = "DeadLetter",
            Attempts = 5,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow,
            IdempotencyKey = new string('C', 64)
        };
        db.CommunicationApprovals.Add(pendingApproval);
        db.CommunicationOutbox.Add(rejectedOutbox);
        await db.SaveChangesAsync();

        var invalidParent = await service.ReplayDeadLetterAsync(
            rejectedOutbox.OutboxId, 1, 21, "Parent was rejected and must remain blocked.", true, httpContext);
        Assert.Equal(CommunicationOutboxReplayResult.InvalidApprovalState, invalidParent);
        Assert.Equal("DeadLetter", rejectedOutbox.Status);
    }

    private static Company CreateCompany(int id) => new()
    {
        CompanyId = id,
        CompanyName = $"Tenant {id}",
        SettingsJson = "{}",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, Company company, string email) => new()
    {
        UserId = id,
        CompanyId = company.CompanyId,
        Company = company,
        Email = email,
        PasswordHash = "unused-test-hash",
        FirstName = "Test",
        LastName = "User",
        FullName = "Test User",
        Role = 5,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
