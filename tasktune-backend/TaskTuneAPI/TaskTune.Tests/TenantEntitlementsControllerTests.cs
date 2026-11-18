using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Controllers;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantEntitlementsControllerTests
{
    [Fact]
    public async System.Threading.Tasks.Task EndpointReturnsOnlyTheAuthenticatedTenantsPlanAndFeatures()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
        {
            var tenantOne = CreateCompany(1);
            var tenantTwo = CreateCompany(2);
            var plan = new SubscriptionPlan
            {
                PlanId = 1, Code = "starter", Name = "Starter", IsActive = true,
                MaxUsers = 25, MaxStorageBytes = 1_000_000, MonthlyAiTokenLimit = 50_000,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            seed.Companies.AddRange(tenantOne, tenantTwo);
            seed.SubscriptionPlans.Add(plan);
            seed.TenantSubscriptions.AddRange(CreateSubscription(tenantOne, plan), CreateSubscription(tenantTwo, plan));
            seed.SubscriptionPlanFeatures.Add(new SubscriptionPlanFeature
            {
                PlanId = 1, FeatureCode = "ai.chat", IsEnabled = true, LimitValue = 10
            });
            await seed.SaveChangesAsync();
        }

        await using var tenantDb = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var identity = new ClaimsIdentity(
            [new Claim("sub", "10"), new Claim("companyId", "1")], "UnitTest");
        var controller = new TenantEntitlementsController(new TenantEntitlementService(tenantDb))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.GetMyEntitlements(CancellationToken.None));
        Assert.NotNull(result.Value);
        var summary = await new TenantEntitlementService(tenantDb).GetTenantSummaryAsync(1);
        Assert.Equal("starter", summary.PlanCode);
        Assert.True(summary.HasActiveSubscription);
        Assert.Equal(25, summary.MaxUsers);
        Assert.Contains(summary.Features, feature =>
            feature.FeatureCode == "ai.chat" && feature.Enabled && feature.LimitValue == 10);

        // The context's global tenant filter prevents this service from reading tenant two.
        var foreign = await new TenantEntitlementService(tenantDb).GetTenantSummaryAsync(2);
        Assert.Null(foreign.PlanCode);
        Assert.Empty(foreign.Features);
    }

    [Fact]
    public async System.Threading.Tasks.Task ExpiredSubscriptionSummaryShowsNoEnabledFeaturesOrCapacity()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = CreateCompany(1);
        var plan = new SubscriptionPlan
        {
            PlanId = 1, Code = "expired", Name = "Expired", IsActive = true,
            MaxUsers = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.SubscriptionPlans.Add(plan);
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            CompanyId = 1, Company = company, PlanId = 1, Plan = plan,
            Status = "Active", StartsAt = DateTime.UtcNow.AddDays(-10),
            EndsAt = DateTime.UtcNow.AddMinutes(-1), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.SubscriptionPlanFeatures.Add(new SubscriptionPlanFeature
        {
            PlanId = 1, FeatureCode = "ai.chat", IsEnabled = true
        });
        await db.SaveChangesAsync();

        var summary = await new TenantEntitlementService(db).GetTenantSummaryAsync(1);
        Assert.False(summary.HasActiveSubscription);
        Assert.Null(summary.MaxUsers);
        Assert.All(summary.Features, feature => Assert.False(feature.Enabled));
    }

    private static Company CreateCompany(int id) => new()
    {
        CompanyId = id, CompanyName = $"Tenant {id}", SettingsJson = "{}",
        Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static TenantSubscription CreateSubscription(Company company, SubscriptionPlan plan) => new()
    {
        CompanyId = company.CompanyId, Company = company, PlanId = plan.PlanId, Plan = plan,
        Status = "Active", StartsAt = DateTime.UtcNow.AddDays(-1),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
