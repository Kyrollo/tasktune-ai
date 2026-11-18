using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Controllers;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantUsageControllerTests
{
    [Fact]
    public async System.Threading.Tasks.Task UsageEndpointUsesCurrentTenantContextNotClientSuppliedCompanyId()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
        {
            seed.Companies.AddRange(CreateCompany(1), CreateCompany(2));
            await seed.SaveChangesAsync();
        }

        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var identity = new ClaimsIdentity(
            [new Claim("sub", "10"), new Claim("companyId", "1")], "UnitTest");
        var controller = new TenantUsageController(new TenantUsageMeterService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.GetCurrentMonth(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, CancellationToken.None));
        var summary = Assert.IsType<TenantUsageSummary>(result.Value);
        Assert.Equal(1, summary.CompanyId);
        Assert.False(summary.AiTokenUsageAvailable);
        Assert.Null(summary.AiTokensUsed);
    }

    [Fact]
    public async System.Threading.Tasks.Task UsageEndpointRejectsInvalidPeriod()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var controller = new TenantUsageController(new TenantUsageMeterService(db));
        var result = Assert.IsType<BadRequestObjectResult>(await controller.GetCurrentMonth(2026, 13, CancellationToken.None));
        Assert.NotNull(result.Value);
    }

    private static Company CreateCompany(int id) => new()
    {
        CompanyId = id, CompanyName = $"Usage tenant {id}", SettingsJson = "{}",
        Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
