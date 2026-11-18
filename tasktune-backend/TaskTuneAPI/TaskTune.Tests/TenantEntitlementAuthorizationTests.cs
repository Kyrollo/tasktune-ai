using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantEntitlementAuthorizationTests
{
    [Fact]
    public async System.Threading.Tasks.Task FeatureGateAllowsEnabledFeatureOnActiveSubscription()
    {
        var db = await CreateContextAsync("Active", true, true);
        await using (db)
        using (var scope = BuildScope(db))
        {
            var filter = new RequiresTenantFeatureAttribute("AI.Chat");
            var context = CreateFilterContext(scope);
            await filter.OnAuthorizationAsync(context);
            Assert.Null(context.Result);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task FeatureGateFailsClosedWithoutSubscriptionOrFeature()
    {
        var noSubscription = await CreateContextAsync(null, false, true);
        await using (noSubscription)
        using (var scope = BuildScope(noSubscription))
        {
            var filter = new RequiresTenantFeatureAttribute("ai.chat");
            var context = CreateFilterContext(scope);
            await filter.OnAuthorizationAsync(context);
            var denied = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
        }

        var featureDisabled = await CreateContextAsync("Active", true, false);
        await using (featureDisabled)
        using (var scope = BuildScope(featureDisabled))
        {
            var filter = new RequiresTenantFeatureAttribute("ai.chat");
            var context = CreateFilterContext(scope);
            await filter.OnAuthorizationAsync(context);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task ExpiredAndPastDueSubscriptionsAreDenied()
    {
        foreach (var status in new[] { "PastDue", "Cancelled" })
        {
            var db = await CreateContextAsync(status, true, true);
            await using (db)
            using (var scope = BuildScope(db))
            {
                var access = await scope.GetRequiredService<TenantEntitlementService>()
                    .GetFeatureAccessAsync(1, "ai.chat");
                Assert.False(access.Enabled);
            }
        }

        var expired = await CreateContextAsync("Active", true, true, endsAt: DateTime.UtcNow.AddMinutes(-1));
        await using (expired)
        {
            var access = await new TenantEntitlementService(expired).GetFeatureAccessAsync(1, "ai.chat");
            Assert.False(access.Enabled);
        }
    }

    private static async System.Threading.Tasks.Task<TaskTuneDbContext> CreateContextAsync(
        string? subscriptionStatus, bool includeFeature, bool featureEnabled,
        DateTime? endsAt = null)
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company
        {
            CompanyId = 1, CompanyName = "Entitlement tenant", SettingsJson = "{}",
            Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var plan = new SubscriptionPlan
        {
            PlanId = 1, Code = "test", Name = "Test", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.SubscriptionPlans.Add(plan);
        if (subscriptionStatus != null)
        {
            db.TenantSubscriptions.Add(new TenantSubscription
            {
                CompanyId = 1, Company = company, PlanId = 1, Plan = plan,
                Status = subscriptionStatus, StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = endsAt, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }
        if (includeFeature)
        {
            db.SubscriptionPlanFeatures.Add(new SubscriptionPlanFeature
            {
                PlanId = 1, FeatureCode = "ai.chat", IsEnabled = featureEnabled
            });
        }
        await db.SaveChangesAsync();
        return db;
    }

    private static ServiceProvider BuildScope(TaskTuneDbContext db) =>
        new ServiceCollection().AddScoped(_ => db).AddScoped<TenantEntitlementService>().BuildServiceProvider();

    private static AuthorizationFilterContext CreateFilterContext(IServiceProvider services)
    {
        var identity = new ClaimsIdentity(
            [new Claim("companyId", "1")], "UnitTest");
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services
        };
        var actionContext = new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
