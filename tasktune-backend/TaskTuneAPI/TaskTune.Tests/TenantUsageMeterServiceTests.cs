using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantUsageMeterServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task SummaryMeasuresUsersInvitationsStorageAndProviderReportedTokens()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
        {
            var company = CreateCompany(1);
            var user = CreateUser(10, company);
            var otherUser = CreateUser(11, company);
            var plan = new SubscriptionPlan
            {
                PlanId = 1, Code = "starter", Name = "Starter", IsActive = true,
                MaxUsers = 3, MaxStorageBytes = 10000, MonthlyAiTokenLimit = 50000,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            var project = new Project
            {
                ProjectId = 100, CompanyId = 1, Company = company,
                ProjectName = "Usage project", ManagerId = 10, Manager = user,
                Status = 0, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            var task = new TaskTune.Infrastructure.Models.Task
            {
                TaskId = 200, ProjectId = 100, Project = project, TaskName = "Usage task",
                Priority = 1, Status = 0, AssignedTo = 10, AssignedToNavigation = user,
                CreatedBy = 10, CreatedByNavigation = user, StartDate = new DateOnly(2026, 1, 1),
                DueDate = new DateOnly(2026, 12, 1), CompletionPercentage = 0,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            seed.Companies.Add(company);
            seed.Users.AddRange(user, otherUser);
            seed.SubscriptionPlans.Add(plan);
            seed.TenantSubscriptions.Add(new TenantSubscription
            {
                CompanyId = 1, Company = company, PlanId = 1, Plan = plan,
                Status = "Active", StartsAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            seed.AccountSecurityTokens.Add(new AccountSecurityToken
            {
                CompanyId = 1, Email = "invite@tenant.test", Purpose = "Invitation",
                TokenHash = new string('A', 64), CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            seed.Projects.Add(project);
            seed.Tasks.Add(task);
            seed.TaskAttachments.Add(new TaskAttachment
            {
                AttachmentId = 300, TaskId = 200, Task = task, UploadedBy = 10,
                UploadedByNavigation = user, FileName = "spec.pdf", FilePath = "private/spec.pdf",
                FileSize = 4096, MimeType = "application/pdf", UploadedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var service = new TenantUsageMeterService(db);
        var recordedAt = DateTime.UtcNow;
        Assert.Equal(UsageRecordResult.Recorded,
            await service.RecordAiTokensAsync(1200, "chat-request-00000001", "chat_assistant", recordedAt));
        Assert.Equal(UsageRecordResult.Duplicate,
            await service.RecordAiTokensAsync(1200, "chat-request-00000001", "chat_assistant", recordedAt));
        Assert.Equal(UsageRecordResult.IdempotencyConflict,
            await service.RecordAiTokensAsync(1300, "chat-request-00000001", "chat_assistant", recordedAt));

        var summary = await service.GetCurrentTenantSummaryAsync(recordedAt.Year, recordedAt.Month);
        Assert.Equal(2, summary.ActiveUsers);
        Assert.Equal(1, summary.PendingInvitations);
        Assert.Equal(3, summary.SeatsReserved);
        Assert.Equal(4096, summary.TaskAttachmentStorageBytes);
        Assert.Equal(1200, summary.AiTokensUsed);
        Assert.True(summary.AiTokenUsageAvailable);
        Assert.Equal(50000, summary.MonthlyAiTokenLimit);
        Assert.False(summary.SeatLimitExceeded);
        Assert.False(summary.StorageLimitExceeded);
    }

    [Fact]
    public async System.Threading.Tasks.Task MissingTenantContextCannotRecordOrReadTenantUsage()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options);
        var service = new TenantUsageMeterService(db);
        Assert.Equal(UsageRecordResult.MissingTenantContext,
            await service.RecordAiTokensAsync(100, "ai-usage-idempotency-0001", "chat", DateTime.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetCurrentTenantSummaryAsync(DateTime.UtcNow.Year, DateTime.UtcNow.Month));
    }

    private static Company CreateCompany(int id) => new()
    {
        CompanyId = id, CompanyName = $"Company {id}", SettingsJson = "{}", Status = "Active",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, Company company) => new()
    {
        UserId = id, CompanyId = company.CompanyId, Company = company,
        Email = $"user{id}@tenant.test", PasswordHash = "test-hash",
        FirstName = "Usage", LastName = "Tester", FullName = "Usage Tester",
        Role = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
