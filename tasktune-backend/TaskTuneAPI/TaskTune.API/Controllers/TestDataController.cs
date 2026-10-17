using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TestDataController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    public TestDataController(TaskTuneDbContext db) => _db = db;

    [HttpGet("raw-users")]
    public IActionResult RawUsers()
    {
        var users = _db.Users.Select(u => new { u.UserId, u.Email, u.FirstName, u.LastName, u.CompanyId }).Take(10).ToList();
        return Ok(new { count = users.Count, data = users });
    }

    [HttpGet("raw-projects")]
    public IActionResult RawProjects()
    {
        var projects = _db.Projects.Select(p => new { p.ProjectId, p.ProjectName, p.CompanyId }).Take(10).ToList();
        return Ok(new { count = projects.Count, data = projects });
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DebugController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    public DebugController(TaskTuneDbContext db) => _db = db;

    [HttpGet("ping-db")]
    public IActionResult PingDb()
    {
        var count = _db.Users.Count();
        return Ok(new { UsersCount = count });
    }
}