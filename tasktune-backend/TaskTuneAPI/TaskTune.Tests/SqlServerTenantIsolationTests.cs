using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
