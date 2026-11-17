using Microsoft.Data.SqlClient;
using Xunit;

namespace TaskTune.Tests;

public sealed class WorkforceSqlMigrationIntegrationTests
{
    [Fact]
    public async Task WorkforceMigrationCreatesTenantScopedTablesAndConstraints()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("TASKTUNE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            Console.WriteLine("TASKTUNE_TEST_SQLSERVER is not configured; SQL Server migration integration test is skipped.");
            return;
        }

        var databaseName = "TaskTuneHRTest_" + Guid.NewGuid().ToString("N");
        var adminBuilder = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = "master" };
        try
        {
            await using (var admin = new SqlConnection(adminBuilder.ConnectionString))
            {
                await admin.OpenAsync();
                await using var create = admin.CreateCommand();
                create.CommandText = $"CREATE DATABASE [{databaseName}]";
                await create.ExecuteNonQueryAsync();
            }

            var appBuilder = new SqlConnectionStringBuilder(configuredConnection) { InitialCatalog = databaseName };
            await using (var connection = new SqlConnection(appBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await ExecuteAsync(connection, """
                    CREATE TABLE dbo.Companies (CompanyId int NOT NULL PRIMARY KEY);
                    CREATE TABLE dbo.Users (UserId int NOT NULL PRIMARY KEY, CompanyId int NOT NULL);
                    INSERT INTO dbo.Companies(CompanyId) VALUES (1);
                    INSERT INTO dbo.Users(UserId, CompanyId) VALUES (1, 1);
                    """);

                var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "20261117_AddHRWorkforceLifecycle.sql");
                var migrationSql = await File.ReadAllTextAsync(migrationPath);
                await ExecuteAsync(connection, migrationSql);

                var tables = await ReadTableNamesAsync(connection);
                Assert.Contains("JobApplicants", tables);
                Assert.Contains("OnboardingTasks", tables);
                Assert.Contains("PerformanceGoals", tables);
                Assert.Contains("ExpenseClaims", tables);
                Assert.Contains("CompanyAssets", tables);
                Assert.Contains("BenefitPlans", tables);
                Assert.Contains("BenefitEnrollments", tables);

                await ExecuteAsync(connection, """
                    INSERT INTO dbo.CompanyAssets (CompanyId, AssetTag, Name) VALUES (1, N'ASSET-001', N'Laptop');
                    """);

                await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, """
                    INSERT INTO dbo.CompanyAssets (CompanyId, AssetTag, Name) VALUES (1, N'ASSET-001', N'Duplicate');
                    """));

                await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, """
                    INSERT INTO dbo.ExpenseClaims (CompanyId, UserId, Category, Amount, CurrencyCode, ExpenseDate)
                    VALUES (1, 1, N'Travel', 0, 'EGP', CONVERT(date, SYSUTCDATETIME()));
                    """));

                await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, """
                    INSERT INTO dbo.JobApplicants (CompanyId, CandidateName, JobTitle, CreatedBy, Stage)
                    VALUES (1, N'Candidate', N'Engineer', 1, N'not-a-stage');
                    """));

                var payrollMigrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "20261117_AddPayrollFoundation.sql");
                var payrollMigrationSql = await File.ReadAllTextAsync(payrollMigrationPath);
                await ExecuteAsync(connection, payrollMigrationSql);

                var payrollTables = await ReadTableNamesAsync(connection);
                Assert.Contains("PayrollPeriods", payrollTables);
                Assert.Contains("Payslips", payrollTables);

                await ExecuteAsync(connection, """
                    INSERT INTO dbo.Users(UserId, CompanyId) VALUES (2, 1);
                    INSERT INTO dbo.PayrollPeriods (CompanyId, PeriodStart, PeriodEnd, CurrencyCode, CreatedBy)
                    VALUES (1, '2026-11-01', '2026-11-30', 'EGP', 1);
                    INSERT INTO dbo.Payslips (CompanyId, PayrollPeriodId, UserId, BaseSalary, Allowances, Bonus, IncomeTaxDeduction, SocialInsuranceDeduction, OtherDeductions, CurrencyCode)
                    VALUES (1, 1, 1, 10000, 1000, 0, 1000, 500, 250, 'EGP');
                    """);

                await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, """
                    INSERT INTO dbo.Payslips (CompanyId, PayrollPeriodId, UserId, BaseSalary, Allowances, Bonus, IncomeTaxDeduction, SocialInsuranceDeduction, OtherDeductions, CurrencyCode)
                    VALUES (1, 1, 2, 1000, 0, 0, 900, 200, 0, 'EGP');
                    """));
            }
        }
        finally
        {
            await using var admin = new SqlConnection(adminBuilder.ConnectionString);
            await admin.OpenAsync();
            await using var drop = admin.CreateCommand();
            drop.CommandText = $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END
                """;
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 90;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo'";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }
}
