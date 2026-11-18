using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantSubscriptionAdministrationTests
{
    [Fact]
    public async System.Threading.Tasks.Task PlanCreationAndFeatureSwitchAreValidated()
    {
        await using var db = CreateContext();
        var service = new TenantSubscriptionAdministrationService(db);
        var created = await service.CreatePlanAsync("Pro", "Pro Plan", "Professional plan", 100, 10_000_000, 1_000_000);
        Assert.Equal(PlanAdministrationResult.Success, created.Result);
        Assert.Equal("pro", created.Plan!.Code);
        Assert.Equal(PlanAdministrationResult.AlreadyExists,
            (await service.CreatePlanAsync("pro", "Duplicate", null, null, null, null)).Result);

        var feature = await service.SetFeatureAsync(created.Plan.PlanId, "AI.Chat", true, 25);
        Assert.Equal(PlanAdministrationResult.Success, feature.Result);
        Assert.Equal("ai.chat", feature.Feature!.FeatureCode);
        Assert.Equal(25, feature.Feature.LimitValue);
        Assert.Equal(PlanAdministrationResult.InvalidInput,
            (await service.SetFeatureAsync(created.Plan.PlanId, "bad feature", true, null)).Result);
        Assert.Equal(PlanAdministrationResult.NotFound,
            (await service.SetFeatureAsync(999, "ai.chat", true, null)).Result);
    }

    [Fact]
    public async System.Threading.Tasks.Task SubscriptionAssignmentWritesHistoryAndChangesCurrentPlan()
    {
        await using var db = CreateContext();
        var company = CreateCompany(1);
        var actor = CreateUser(10, company);
        var planA = CreatePlan(1, "starter");
        var planB = CreatePlan(2, "pro");
        db.Companies.Add(company);
        db.Users.Add(actor);
        db.SubscriptionPlans.AddRange(planA, planB);
        await db.SaveChangesAsync();

        var service = new TenantSubscriptionAdministrationService(db);
        var start = DateTime.UtcNow.AddMinutes(-1);
        Assert.Equal(PlanAdministrationResult.Success,
            await service.AssignSubscriptionAsync(1, 10, 1, "Trial", start, null,
                "Trial access approved by platform operations."));
        Assert.Equal(PlanAdministrationResult.Success,
            await service.AssignSubscriptionAsync(1, 10, 2, "Active", start, null,
                "Plan upgrade approved by platform operations."));

        var subscription = await db.TenantSubscriptions.SingleAsync();
        Assert.Equal(2, subscription.PlanId);
        Assert.Equal("Active", subscription.Status);
        var history = await db.TenantSubscriptionEvents.OrderBy(item => item.EventId).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("Assigned", history[0].Action);
        Assert.Equal("PlanChanged", history[1].Action);
        Assert.Equal(1, history[1].PreviousPlanId);
        Assert.Equal(2, history[1].NewPlanId);
        Assert.All(history, item => Assert.Equal(10, item.ActorUserId));
        Assert.Contains("upgrade", history[1].Reason);
    }

    [Fact]
    public async System.Threading.Tasks.Task AssignmentRejectsInvalidActorPlanStatusPeriodAndReason()
    {
        await using var db = CreateContext();
        var company = CreateCompany(1);
        var actor = CreateUser(10, company);
        var plan = CreatePlan(1, "starter");
        var inactivePlan = CreatePlan(2, "inactive");
        inactivePlan.IsActive = false;
        db.Companies.Add(company);
        db.Users.Add(actor);
        db.SubscriptionPlans.AddRange(plan, inactivePlan);
        await db.SaveChangesAsync();

        var service = new TenantSubscriptionAdministrationService(db);
        var start = DateTime.UtcNow;
        Assert.Equal(PlanAdministrationResult.InvalidReason,
            await service.AssignSubscriptionAsync(1, 10, 1, "Trial", start, null, "bad"));
        Assert.Equal(PlanAdministrationResult.ActorNotFound,
            await service.AssignSubscriptionAsync(1, 99, 1, "Trial", start, null,
                "Approved by platform administrator."));
        Assert.Equal(PlanAdministrationResult.NotFound,
            await service.AssignSubscriptionAsync(999, 10, 1, "Trial", start, null,
                "Approved by platform administrator."));
        Assert.Equal(PlanAdministrationResult.InvalidStatus,
            await service.AssignSubscriptionAsync(1, 10, 1, "Unknown", start, null,
                "Approved by platform administrator."));
        Assert.Equal(PlanAdministrationResult.InvalidPeriod,
            await service.AssignSubscriptionAsync(1, 10, 1, "Trial", start, start,
                "Approved by platform administrator."));
        Assert.Equal(PlanAdministrationResult.InactivePlan,
            await service.AssignSubscriptionAsync(1, 10, 2, "Trial", start, null,
                "Approved by platform administrator."));
        Assert.Empty(await db.TenantSubscriptionEvents.ToListAsync());
    }

    private static TaskTuneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        return new TaskTuneDbContext(options, new FixedTenantContext(1));
    }

    private static Company CreateCompany(int id) => new()
    {
        CompanyId = id, CompanyName = $"Company {id}", SettingsJson = "{}",
        Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, Company company) => new()
    {
        UserId = id, CompanyId = company.CompanyId, Company = company,
        Email = $"admin{id}@tenant.test", PasswordHash = "test-hash",
        FirstName = "Platform", LastName = "Admin", FullName = "Platform Admin",
        Role = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static SubscriptionPlan CreatePlan(int id, string code) => new()
    {
        PlanId = id, Code = code, Name = code, IsActive = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
