using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Controllers;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class HealthCheckControllerTests
{
    [Fact]
    public void Live_ReturnsHealthyWithoutCheckingDatabase()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new TaskTuneDbContext(options, new FixedTenantContext(null));
        var controller = new HealthCheckController(db);

        var result = Assert.IsType<OkObjectResult>(controller.Live());
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task Ready_ReturnsHealthyWhenDatabaseCanConnect()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(null));
        var controller = new HealthCheckController(db);

        var result = Assert.IsType<OkObjectResult>(await controller.Ready(CancellationToken.None));
        Assert.NotNull(result.Value);
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
