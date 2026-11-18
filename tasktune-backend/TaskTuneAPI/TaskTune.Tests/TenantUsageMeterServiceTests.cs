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
    public async System.Threading.Tasks.Task PlatformSummaryMeasuresSpecifiedTenantWithoutTenantContext()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options);
        var company = new Company
        {
            CompanyId = 77, CompanyName = "Platform usage tenant", SettingsJson = "{}",
            Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            UserId = 700, CompanyId = 77, Company = company, Email = "usage@platform.test",
            PasswordHash = "test", FirstName = "Usage", LastName = "User", FullName = "Usage User",
            Role = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var plan = new SubscriptionPlan
        {
            PlanId = 7, Code = "platform-usage", Name = "Platform usage", IsActive = true,
            MaxUsers = 5, MaxStorageBytes = 10000, MonthlyAiTokenLimit = 5000,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var project = new Project
        {
            ProjectId = 770, CompanyId = 77, Company = company, ProjectName = "Project",
            ManagerId = 700, Manager = user, Status = 0,
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var task = new TaskTune.Infrastructure.Models.Task
        {
            TaskId = 7700, ProjectId = 770, Project = project, TaskName = "Task",
            Priority = 1, Status = 0, AssignedTo = 700, AssignedToNavigation = user,
            CreatedBy = 700, CreatedByNavigation = user,
            StartDate = new DateOnly(2026, 1, 1), DueDate = new DateOnly(2026, 12, 1),
            CompletionPercentage = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.Users.Add(user);
        db.SubscriptionPlans.Add(plan);
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            CompanyId = 77, Company = company, PlanId = 7, Plan = plan, Status = "Active",
            StartsAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.AccountSecurityTokens.Add(new AccountSecurityToken
        {
            CompanyId = 77, Email = "pending@platform.test", Purpose = "Invitation",
            TokenHash = new string('F', 64), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(12)
        });
        db.Projects.Add(project);
        db.Tasks.Add(task);
        db.TaskAttachments.Add(new TaskAttachment
        {
            AttachmentId = 77000, TaskId = 7700, Task = task, UploadedBy = 700,
            UploadedByNavigation = user, FileName = "usage.pdf", FilePath = "tasks/7700/usage.pdf",
            FileSize = 2048, MimeType = "application/octet-stream", UploadedAt = DateTime.UtcNow
        });
        db.AIActionsLogs.Add(new AIActionsLog
        {
            AiLogId = 770000, ActionType = 1, UserId = 700, User = user,
            TaskId = 7700, Task = task, InputData = "{}", OutputData = "{}",
            ModelVersion = "test", Timestamp = DateTime.UtcNow
        });
        db.TenantUsageEvents.Add(new TenantUsageEvent
        {
            CompanyId = 77, MeterCode = "ai.tokens", Quantity = 1200,
            IdempotencyKey = "platform-tenant-usage-0001", Source = "provider",
            OccurredAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var summary = await new TenantUsageMeterService(db).GetPlatformTenantSummaryAsync(
            77, DateTime.UtcNow.Year, DateTime.UtcNow.Month);
        Assert.Equal(77, summary.CompanyId);
        Assert.Equal("platform-usage", summary.PlanCode);
        Assert.Equal(1, summary.ActiveUsers);
        Assert.Equal(1, summary.PendingInvitations);
        Assert.Equal(2, summary.SeatsReserved);
        Assert.Equal(2048, summary.TaskAttachmentStorageBytes);
        Assert.Equal(1, summary.AiActions);
        Assert.Equal(1200, summary.AiTokensUsed);
        Assert.True(summary.AiTokenUsageAvailable);
    }

    [Fact]
    public async System.Threading.Tasks.Task AddAiTokenUsageEventAddsPositiveProviderUsageToCurrentTenant()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(44));
        db.Companies.Add(new Company
        {
            CompanyId = 44, CompanyName = "Usage tenant", SettingsJson = "{}",
            Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new TenantUsageMeterService(db);

        Assert.Equal(UsageRecordResult.Recorded,
            service.AddAiTokenUsageEvent(4321, "chat-ai-usage-00000001", "chat_assistant", DateTime.UtcNow));
        Assert.Equal(UsageRecordResult.InvalidInput,
            service.AddAiTokenUsageEvent(0, "chat-ai-usage-00000002", "chat_assistant", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var stored = await db.TenantUsageEvents.SingleAsync();
        Assert.Equal(44, stored.CompanyId);
        Assert.Equal("ai.tokens", stored.MeterCode);
        Assert.Equal(4321, stored.Quantity);
        Assert.Equal("chat_assistant", stored.Source);
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
