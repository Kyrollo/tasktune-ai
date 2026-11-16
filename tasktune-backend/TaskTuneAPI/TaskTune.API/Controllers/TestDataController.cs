using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TestDataController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly IHostEnvironment _environment;

    public TestDataController(TaskTuneDbContext db, IHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet("raw-users")]
    public async Task<IActionResult> RawUsers()
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId.Value)
            .Select(u => new { u.UserId, u.Email, u.FirstName, u.LastName, u.CompanyId })
            .Take(10)
            .ToListAsync();
        return Ok(new { count = users.Count, data = users });
    }

    [HttpGet("raw-projects")]
    public async Task<IActionResult> RawProjects()
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var projects = await _db.Projects.AsNoTracking()
            .Where(p => p.CompanyId == companyId.Value)
            .Select(p => new { p.ProjectId, p.ProjectName, p.CompanyId })
            .Take(10)
            .ToListAsync();
        return Ok(new { count = projects.Count, data = projects });
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DebugController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly IHostEnvironment _environment;

    public DebugController(TaskTuneDbContext db, IHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet("ping-db")]
    public async Task<IActionResult> PingDb()
    {
        if (!_environment.IsDevelopment()) return NotFound();
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var count = await _db.Users.CountAsync(u => u.CompanyId == companyId.Value);
        return Ok(new { UsersCount = count, CompanyId = companyId.Value });
    }
}
