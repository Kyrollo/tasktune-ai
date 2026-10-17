using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public SearchController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new { message = "Query must be at least 2 characters." });

        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();

        var term = q.Trim().ToLower();
        var searchPattern = $"%{term}%";

        var users = await _db.Users
            .Where(u => u.IsActive && u.CompanyId == companyId.Value &&
                (EF.Functions.Like(u.FirstName.ToLower(), searchPattern) ||
                 EF.Functions.Like(u.LastName.ToLower(), searchPattern) ||
                 EF.Functions.Like(u.Email.ToLower(), searchPattern)))
            .Take(limit)
            .Select(u => new { type = "user", id = u.UserId, name = u.FullName, email = u.Email })
            .ToListAsync();

        var tasks = await _db.Tasks
            .Where(t => t.Project.CompanyId == companyId.Value &&
                (EF.Functions.Like(t.TaskName.ToLower(), searchPattern) ||
                 (t.Description != null && EF.Functions.Like(t.Description.ToLower(), searchPattern))))
            .Take(limit)
            .Select(t => new { type = "task", id = t.TaskId, name = t.TaskName, description = t.Description })
            .ToListAsync();

        var projects = await _db.Projects
            .Where(p => p.CompanyId == companyId.Value &&
                (EF.Functions.Like(p.ProjectName.ToLower(), searchPattern) ||
                 (p.Description != null && EF.Functions.Like(p.Description.ToLower(), searchPattern))))
            .Take(limit)
            .Select(p => new { type = "project", id = p.ProjectId, name = p.ProjectName, description = p.Description })
            .ToListAsync();

        return Ok(new
        {
            users,
            tasks,
            projects
        });
    }
}
