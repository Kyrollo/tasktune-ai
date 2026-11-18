using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Controllers;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class PlatformTenantsControllerTests
{
    [Fact]
    public async System.Threading.Tasks.Task PlatformAdminCanListAndSuspendAnotherTenantWithAudit()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var first = CreateCompany(1, "Platform home");
        var second = CreateCompany(2, "Customer two");
        var actor = CreateUser(10, first, "platform@tasktune.test");
        var targetUser = CreateUser(20, second, "member@customer.test");
        db.Companies.AddRange(first, second);
        db.Users.AddRange(actor, targetUser);
        await db.SaveChangesAsync();

        var controller = CreateController(db, 10, 1);
        var list = Assert.IsType<OkObjectResult>(await controller.GetTenants());
        Assert.NotNull(list.Value);

        var result = Assert.IsType<OkObjectResult>(await controller.SuspendTenant(
            2, new TenantLifecycleReasonRequest { Reason = "Contractual suspension approved by platform operations." },
            CancellationToken.None));
        Assert.NotNull(result.Value);

        var suspended = await db.Companies.IgnoreQueryFilters().SingleAsync(item => item.CompanyId == 2);
        Assert.Equal("Suspended", suspended.Status);
        var audit = await db.TenantLifecycleEvents.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(2, audit.CompanyId);
        Assert.Equal(10, audit.ActorUserId);
        Assert.Equal("Suspended", audit.Action);
    }

    [Fact]
    public async System.Threading.Tasks.Task PlatformLifecycleEndpointsRejectInvalidReasonsAndMissingTenants()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = CreateCompany(1, "Platform home");
        var actor = CreateUser(10, company, "platform@tasktune.test");
        db.Companies.Add(company);
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        var controller = CreateController(db, 10, 1);

        Assert.IsType<BadRequestObjectResult>(await controller.SuspendTenant(
            1, new TenantLifecycleReasonRequest { Reason = "short" }, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.SuspendTenant(
            999, new TenantLifecycleReasonRequest { Reason = "Reviewed and approved by platform operations." }, CancellationToken.None));
        Assert.Empty(await db.TenantLifecycleEvents.IgnoreQueryFilters().ToListAsync());
    }

    private static PlatformTenantsController CreateController(TaskTuneDbContext db, int actorId, int homeCompanyId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", actorId.ToString()),
            new Claim("companyId", homeCompanyId.ToString()),
            new Claim("platform_admin", "true"),
            new Claim("role", "Admin")
        ], "UnitTest");
        return new PlatformTenantsController(db, new TenantLifecycleService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    private static Company CreateCompany(int id, string name) => new()
    {
        CompanyId = id, CompanyName = name, SettingsJson = "{}", Status = "Active",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, Company company, string email) => new()
    {
        UserId = id, CompanyId = company.CompanyId, Company = company,
        Email = email, PasswordHash = "test-hash",
        FirstName = "Test", LastName = "User", FullName = "Test User",
        Role = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
