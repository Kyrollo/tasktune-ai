using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantStorageCapacityServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task StorageCheckIncludesExistingAttachmentsAndAllowsWithinLimit()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var seed = new TaskTuneDbContext(options, new FixedTenantContext(null));
        var company = CreateCompany();
        var user = CreateUser(company);
        var plan = CreatePlan(maxStorageBytes: 5000);
        var project = new Project
        {
            ProjectId = 20, CompanyId = 1, Company = company, ProjectName = "Storage",
            ManagerId = 10, Manager = user, Status = 0,
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var task = new TaskTune.Infrastructure.Models.Task
        {
            TaskId = 30, ProjectId = 20, Project = project, TaskName = "Attachment task",
            Priority = 1, Status = 0, AssignedTo = 10, AssignedToNavigation = user,
            CreatedBy = 10, CreatedByNavigation = user,
            StartDate = new DateOnly(2026, 1, 1), DueDate = new DateOnly(2026, 12, 1),
            CompletionPercentage = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        seed.Companies.Add(company);
        seed.Users.Add(user);
        seed.Projects.Add(project);
        seed.Tasks.Add(task);
        seed.SubscriptionPlans.Add(plan);
        seed.TenantSubscriptions.Add(CreateSubscription(plan));
        seed.TaskAttachments.Add(new TaskAttachment
        {
            AttachmentId = 40, TaskId = 30, Task = task, UploadedBy = 10,
            UploadedByNavigation = user, FileName = "old.pdf", FilePath = "tasks/30/old.pdf",
            FileSize = 4096, MimeType = "application/octet-stream", UploadedAt = DateTime.UtcNow
        });
        await seed.SaveChangesAsync();

        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var service = new TenantStorageCapacityService(db);
        var allowed = await service.CheckAndLockAsync(1, 512);
        Assert.Equal(TenantStorageCapacityResult.Allowed, allowed.Result);
        Assert.Equal(4096, allowed.CurrentStorageBytes);
        Assert.Equal(5000, allowed.MaxStorageBytes);

        var denied = await service.CheckAndLockAsync(1, 1024);
        Assert.Equal(TenantStorageCapacityResult.LimitExceeded, denied.Result);
    }

    [Fact]
    public async System.Threading.Tasks.Task StorageCheckRequiresCurrentTenantAndActiveSubscription()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var empty = new TaskTuneDbContext(options);
        var service = new TenantStorageCapacityService(empty);
        Assert.Equal(TenantStorageCapacityResult.TenantContextMismatch,
            (await service.CheckAndLockAsync(1, 100)).Result);

        await using var tenant = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var denied = await new TenantStorageCapacityService(tenant).CheckAndLockAsync(1, 100);
        Assert.Equal(TenantStorageCapacityResult.NoActiveSubscription, denied.Result);
    }

    private static Company CreateCompany() => new()
    {
        CompanyId = 1, CompanyName = "Storage tenant", SettingsJson = "{}", Status = "Active",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User CreateUser(Company company) => new()
    {
        UserId = 10, CompanyId = 1, Company = company, Email = "storage@tenant.test",
        PasswordHash = "test", FirstName = "Storage", LastName = "Admin", FullName = "Storage Admin",
        Role = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static SubscriptionPlan CreatePlan(long? maxStorageBytes) => new()
    {
        PlanId = 1, Code = "storage-plan", Name = "Storage plan", IsActive = true,
        MaxStorageBytes = maxStorageBytes, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static TenantSubscription CreateSubscription(SubscriptionPlan plan) => new()
    {
        CompanyId = 1, PlanId = 1, Plan = plan, Status = "Active",
        StartsAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
