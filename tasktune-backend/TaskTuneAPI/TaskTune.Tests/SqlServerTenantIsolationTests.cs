using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
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
            await tenantOne.UserSessions.CountAsync();
            await tenantOne.AccountSecurityTokens.CountAsync();
            await tenantOne.CommunicationApprovals.CountAsync();
            await tenantOne.CommunicationOutbox.CountAsync();
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
    public async System.Threading.Tasks.Task SqlServer_CommunicationApprovalMigrationIsIdempotent()
    {
        var source = Environment.GetEnvironmentVariable("TASKTUNE_SQLSERVER_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(source),
            "TASKTUNE_SQLSERVER_TEST_CONNECTION must be configured by the SQL Server CI service.");

        var databaseName = $"TaskTuneCommunicationMigration_{Guid.NewGuid():N}";
        var masterConnection = new SqlConnectionStringBuilder(source!)
        {
            InitialCatalog = "master"
        };

        await using (var master = new SqlConnection(masterConnection.ConnectionString))
        {
            await master.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{databaseName}]", master);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var targetConnection = new SqlConnectionStringBuilder(masterConnection.ConnectionString)
            {
                InitialCatalog = databaseName
            };
            await using var connection = new SqlConnection(targetConnection.ConnectionString);
            await connection.OpenAsync();

            const string prerequisiteSchema = """
                CREATE TABLE dbo.Companies (CompanyId INT NOT NULL PRIMARY KEY);
                CREATE TABLE dbo.Users (UserId INT NOT NULL PRIMARY KEY);
                CREATE TABLE dbo.Departments (DepartmentId INT NOT NULL PRIMARY KEY);
                CREATE TABLE dbo.Positions (PositionId INT NOT NULL PRIMARY KEY);
                CREATE TABLE dbo.CommunicationLog (CommunicationId INT NOT NULL PRIMARY KEY);
                """;
            await using (var prerequisites = new SqlCommand(prerequisiteSchema, connection))
                await prerequisites.ExecuteNonQueryAsync();

            var migrationRelativePath = Path.Combine(
                "tasktune-backend",
                "Script Database",
                "Migrations",
                "20261117_003_communication_approval_outbox.sql");
            var searchDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            string? migrationPath = null;
            while (searchDirectory != null)
            {
                var candidate = Path.Combine(searchDirectory.FullName, migrationRelativePath);
                if (File.Exists(candidate))
                {
                    migrationPath = candidate;
                    break;
                }
                searchDirectory = searchDirectory.Parent;
            }
            Assert.False(string.IsNullOrWhiteSpace(migrationPath),
                $"Unable to locate the migration script from {AppContext.BaseDirectory}.");
            var script = await File.ReadAllTextAsync(migrationPath);
            var batches = Regex.Split(script, @"^\s*GO\s*$",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(batch => !string.IsNullOrWhiteSpace(batch));

            foreach (var batch in batches)
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync();
            }

            // Re-execution must be a no-op, including all named indexes and constraints.
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$",
                         RegexOptions.Multiline | RegexOptions.IgnoreCase)
                     .Where(batch => !string.IsNullOrWhiteSpace(batch)))
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync();
            }

            const string verifySql = """
                SELECT
                    CASE WHEN OBJECT_ID(N'dbo.CommunicationApprovals', N'U') IS NULL THEN 0 ELSE 1 END,
                    CASE WHEN OBJECT_ID(N'dbo.CommunicationOutbox', N'U') IS NULL THEN 0 ELSE 1 END,
                    CASE WHEN COL_LENGTH(N'dbo.CommunicationApprovals', N'IdempotencyKey') IS NULL THEN 0 ELSE 1 END,
                    (SELECT COUNT(*) FROM sys.foreign_keys
                     WHERE name = N'FK_CommunicationOutbox_Communications'
                       AND referenced_object_id = OBJECT_ID(N'dbo.CommunicationLog'));
                """;
            await using var verify = new SqlCommand(verifySql, connection);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(1, reader.GetInt32(1));
            Assert.Equal(1, reader.GetInt32(2));
            Assert.Equal(1, reader.GetInt32(3));
        }
        finally
        {
            await using var cleanup = new SqlConnection(masterConnection.ConnectionString);
            await cleanup.OpenAsync();
            var safeName = databaseName.Replace("]", "]]", StringComparison.Ordinal);
            await using var drop = new SqlCommand(
                $"ALTER DATABASE [{safeName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{safeName}];",
                cleanup);
            await drop.ExecuteNonQueryAsync();
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
