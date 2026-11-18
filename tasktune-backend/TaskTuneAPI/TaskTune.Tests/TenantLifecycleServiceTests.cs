using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantLifecycleServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task SuspendAndReactivate_AreAuditedAndCannotBeRepeated()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company { CompanyId = 1, CompanyName = "Lifecycle tenant", SettingsJson = "{}", Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var actor = new User { UserId = 10, CompanyId = 1, Company = company, Email = "operator@tenant.test", PasswordHash = "test", FirstName = "Platform", LastName = "Operator", FullName = "Platform Operator", Role = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Companies.Add(company); db.Users.Add(actor); await db.SaveChangesAsync();

        var service = new TenantLifecycleService(db);
        Assert.Equal(TenantLifecycleChangeResult.Success, await service.SuspendAsync(1, 10, "Contractual suspension approved."));
        Assert.Equal(TenantLifecycleChangeResult.AlreadyInState, await service.SuspendAsync(1, 10, "Duplicate request recorded for testing."));
        Assert.Equal("Suspended", (await db.Companies.SingleAsync()).Status);
        Assert.NotNull((await db.Companies.SingleAsync()).SuspendedAt);
        Assert.Equal(TenantLifecycleChangeResult.Success, await service.ReactivateAsync(1, 10, "Outstanding compliance requirements resolved."));
        var active = await db.Companies.SingleAsync();
        Assert.Equal("Active", active.Status); Assert.Null(active.SuspendedAt);
        var events = await db.TenantLifecycleEvents.OrderBy(item => item.EventId).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Equal(new[] { "Suspended", "Reactivated" }, events.Select(item => item.Action));
        Assert.Equal(new[] { "Active", "Suspended" }, events.Select(item => item.PreviousStatus));
        Assert.Equal(new[] { "Suspended", "Active" }, events.Select(item => item.NewStatus));
        Assert.All(events, item => Assert.Equal(10, item.ActorUserId));
    }

    [Fact]
    public async System.Threading.Tasks.Task LifecycleChangesRejectMissingActorsReasonsAndUnknownTenants()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company { CompanyId = 1, CompanyName = "Lifecycle tenant", SettingsJson = "{}", Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var actor = new User { UserId = 10, CompanyId = 1, Company = company, Email = "operator@tenant.test", PasswordHash = "test", FirstName = "Platform", LastName = "Operator", FullName = "Platform Operator", Role = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Companies.Add(company);
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        var service = new TenantLifecycleService(db);
        Assert.Equal(TenantLifecycleChangeResult.InvalidReason, await service.SuspendAsync(1, 10, "short"));
        Assert.Equal(TenantLifecycleChangeResult.ActorNotFound, await service.SuspendAsync(1, 11, "Reviewed and approved by platform operations."));
        Assert.Equal(TenantLifecycleChangeResult.NotFound, await service.SuspendAsync(2, 10, "Reviewed and approved by platform operations."));
        Assert.Empty(await db.TenantLifecycleEvents.ToListAsync());
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
