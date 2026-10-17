using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthCheckController : ControllerBase
{
    private readonly TaskTuneDbContext _db;

    public HealthCheckController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            return Ok(new
            {
                status = canConnect ? "Healthy" : "Degraded",
                database = canConnect,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                database = false,
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
