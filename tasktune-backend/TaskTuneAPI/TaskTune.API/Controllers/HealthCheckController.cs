using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public class HealthCheckController : ControllerBase
{
    private readonly TaskTuneDbContext _db;

    public HealthCheckController(TaskTuneDbContext db) => _db = db;

    // Liveness must not depend on SQL Server or another downstream service.
    [HttpGet("live")]
    public IActionResult Live() => Ok(new
    {
        status = "Healthy",
        timestamp = DateTime.UtcNow
    });

    // Readiness is dependency-aware and is suitable for traffic-routing probes.
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "Degraded",
                    timestamp = DateTime.UtcNow
                });
            }

            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Do not expose connection strings, provider errors, or infrastructure details.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                timestamp = DateTime.UtcNow
            });
        }
    }

    // Backward-compatible endpoint; preserve the existing database readiness semantics.
    [HttpGet]
    public Task<IActionResult> Check(CancellationToken cancellationToken) => Ready(cancellationToken);
}
