using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

/// <summary>
/// Runs against a real SQL Server in CI so tenant filters are translated and enforced by
/// the production relational provider, not only by EF Core InMemory.
/// </summary>
public sealed class SqlServerTenantIsolationTests
{
    [Fact]
    public async System.Threading.Tasks.Task SqlServer_EnforcesCompanyAndUserTenantFilters()
    {
        var source = Environment.GetEnvironmentVariable("TASKTUNE_SQLSERVER_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(source),
            "TASKTUNE_SQLSERVER_TEST_CONNECTION must be configured by the SQL Server CI service.");

        var connection = new SqlConnectionStringBuilder(source!)
        {
            InitialCatalog = $"TaskTuneTenantFilter_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;

        try
        {
            int companyOneId;
            int companyTwoId;

            await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
            {
                await seed.Database.EnsureCreatedAsync();

                var companyOne = new Company
                {
                    CompanyName = "Tenant One",
                    SettingsJson = "{}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var companyTwo = new Company
                {
                    CompanyName = "Tenant Two",
                    SettingsJson = "{}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                seed.Companies.AddRange(companyOne, companyTwo);
                await seed.SaveChangesAsync();

                companyOneId = companyOne.CompanyId;
                companyTwoId = companyTwo.CompanyId;

                seed.Users.AddRange(
                    CreateUser(companyOne, "one@tenant.test"),
                    CreateUser(companyTwo, "two@tenant.test"));
                await seed.SaveChangesAsync();
            }

            await using (var tenantOne = new TaskTuneDbContext(options, new FixedTenantContext(companyOneId)))
            {
                Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", tenantOne.Database.ProviderName);
                Assert.Equal(new[] { companyOneId },
                    await tenantOne.Companies.Select(c => c.CompanyId).ToArrayAsync());
                Assert.Equal(new[] { "one@tenant.test" },
                    await tenantOne.Users.Select(u => u.Email).ToArrayAsync());
                Assert.Equal(2, await tenantOne.Users.IgnoreQueryFilters().CountAsync());

            // Force SQL Server to translate every configured tenant filter, including filters
            // that traverse task/project/user relationships.
            await tenantOne.Departments.CountAsync();
            await tenantOne.Positions.CountAsync();
            await tenantOne.Projects.CountAsync();
            await tenantOne.RolePermissions.CountAsync();
            await tenantOne.Reports.CountAsync();
            await tenantOne.CommunicationLogs.CountAsync();
            await tenantOne.CommunicationApprovals.CountAsync();
            await tenantOne.CommunicationOutbox.CountAsync();
            await tenantOne.Tasks.CountAsync();
            await tenantOne.Subtasks.CountAsync();
            await tenantOne.TaskComments.CountAsync();
            await tenantOne.TaskAccessControls.CountAsync();
            await tenantOne.TaskDependencies.CountAsync();
            await tenantOne.TaskProgresses.CountAsync();
            await tenantOne.TaskAttachments.CountAsync();
            await tenantOne.Evaluations.CountAsync();
            await tenantOne.Bugs.CountAsync();
            await tenantOne.Notifications.CountAsync();
            await tenantOne.AuditLogs.CountAsync();
            await tenantOne.AIActionsLogs.CountAsync();
            await tenantOne.ChatThreads.CountAsync();
            await tenantOne.ChatMessages.CountAsync();
            await tenantOne.UserSettings.CountAsync();
            await tenantOne.UserSessions.CountAsync();
            await tenantOne.AccountSecurityTokens.CountAsync();
            }

            await using (var tenantTwo = new TaskTuneDbContext(options, new FixedTenantContext(companyTwoId)))
            {
                Assert.Equal(new[] { companyTwoId },
                    await tenantTwo.Companies.Select(c => c.CompanyId).ToArrayAsync());
                Assert.Equal(new[] { "two@tenant.test" },
                    await tenantTwo.Users.Select(u => u.Email).ToArrayAsync());
            }

            await using (var unscoped = new TaskTuneDbContext(options, new FixedTenantContext(null)))
            {
                Assert.Empty(await unscoped.Companies.ToListAsync());
                Assert.Empty(await unscoped.Users.ToListAsync());
            }
        }
        finally
        {
            await using var cleanup = new TaskTuneDbContext(options, new FixedTenantContext(null));
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task OutboxClaim_IsAtomicAcrossWorkers_AndExpiredLeaseCanBeReclaimed()
    {
        var source = Environment.GetEnvironmentVariable("TASKTUNE_SQLSERVER_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(source),
            "TASKTUNE_SQLSERVER_TEST_CONNECTION must be configured by the SQL Server CI service.");
        var connection = new SqlConnectionStringBuilder(source!)
        {
            InitialCatalog = $"TaskTuneOutboxClaim_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;

        try
        {
            long outboxId;
            int companyId;
            await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
            {
                await seed.Database.EnsureCreatedAsync();
                var company = new Company
                {
                    CompanyName = "Outbox Claim Tenant", SettingsJson = "{}",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                };
                seed.Companies.Add(company);
                await seed.SaveChangesAsync();
                companyId = company.CompanyId;

                var requester = CreateUser(company, "outbox-owner@tenant.test");
                seed.Users.Add(requester);
                await seed.SaveChangesAsync();

                var approval = new CommunicationApproval
                {
                    Company = company, CompanyId = companyId,
                    RequestedByUser = requester, RequestedByUserId = requester.UserId,
                    CommunicationType = 1, RecipientType = "Individual",
                    RecipientEmailsJson = "[\"outbox-recipient@tenant.test\"]",
                    CcEmailsJson = "[]", Subject = "Claim test", Body = "Test",
                    Status = "Approved", IdempotencyKey = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                };
                seed.CommunicationApprovals.Add(approval);
                await seed.SaveChangesAsync();

                var item = new CommunicationOutbox
                {
                    Approval = approval, ApprovalId = approval.ApprovalId, CompanyId = companyId,
                    RecipientEmail = "outbox-recipient@tenant.test", CcEmailsJson = "[]",
                    Subject = "Claim test", Body = "Test", Status = "Pending",
                    Attempts = 0, NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
                    CreatedAt = DateTime.UtcNow, IdempotencyKey = new string('B', 64)
                };
                seed.CommunicationOutbox.Add(item);
                await seed.SaveChangesAsync();
                outboxId = item.OutboxId;
            }

            await using var workerOne = new TaskTuneDbContext(options, new FixedTenantContext(companyId));
            await using var workerTwo = new TaskTuneDbContext(options, new FixedTenantContext(companyId));
            var claims = await System.Threading.Tasks.Task.WhenAll(
                CommunicationOutboxWorker.TryClaimNextAsync(workerOne, 60),
                CommunicationOutboxWorker.TryClaimNextAsync(workerTwo, 60));
            var claim = Assert.Single(claims.Where(x => x != null));
            Assert.Equal(outboxId, claim!.OutboxId);
            Assert.Equal("Processing", claim.Status);
            Assert.Equal(1, claim.Attempts);
            Assert.NotNull(claim.LeaseToken);

            var oldLease = claim.LeaseToken;
            await workerOne.CommunicationOutbox.IgnoreQueryFilters()
                .Where(x => x.OutboxId == outboxId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LeaseExpiresAt, DateTime.UtcNow.AddSeconds(-10)));

            var reclaimed = await CommunicationOutboxWorker.TryClaimNextAsync(workerTwo, 60);
            Assert.NotNull(reclaimed);
            Assert.Equal(outboxId, reclaimed.OutboxId);
            Assert.Equal(2, reclaimed.Attempts);
            Assert.NotEqual(oldLease, reclaimed.LeaseToken);

            // Expired leases at the retry limit are not reclaimed forever. They are
            // moved to DeadLetter for operator reconciliation because SMTP acceptance
            // may have occurred before the prior worker lost its lease.
            await workerOne.CommunicationOutbox.IgnoreQueryFilters()
                .Where(x => x.OutboxId == outboxId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Attempts, 5)
                    .SetProperty(x => x.LeaseExpiresAt, DateTime.UtcNow.AddSeconds(-10)));

            var exhaustedClaim = await CommunicationOutboxWorker.TryClaimNextAsync(
                workerTwo, 60, maxAttempts: 5);
            Assert.Null(exhaustedClaim);

            var deadLettered = await CommunicationOutboxWorker.MoveExhaustedItemsToDeadLetterAsync(
                workerOne, maxAttempts: 5);
            Assert.Equal(1, deadLettered);

            var exhaustedItem = await workerTwo.CommunicationOutbox.IgnoreQueryFilters()
                .SingleAsync(x => x.OutboxId == outboxId);
            Assert.Equal("DeadLetter", exhaustedItem.Status);
            Assert.Null(exhaustedItem.LeaseToken);
            Assert.Null(exhaustedItem.LeaseExpiresAt);
        }
        finally
        {
            await using var cleanup = new TaskTuneDbContext(options, new FixedTenantContext(null));
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static User CreateUser(Company company, string email) => new()
    {
        Company = company,
        CompanyId = company.CompanyId,
        Email = email,
        PasswordHash = "integration-test-only-hash",
        FirstName = "Integration",
        LastName = "Test",
        Role = 0,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
