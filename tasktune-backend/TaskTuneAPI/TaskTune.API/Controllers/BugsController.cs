using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BugsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public BugsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetBugs([FromQuery] int? taskId)
    {
        var query = _db.Bugs.AsQueryable();
        if (taskId.HasValue)
            query = query.Where(b => b.TaskId == taskId.Value);

        var items = await query
            .Include(b => b.ReportedByNavigation)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.BugId,
                b.TaskId,
                b.ReportedBy,
                ReportedByName = b.ReportedByNavigation.FirstName + " " + b.ReportedByNavigation.LastName,
                b.BugDescription,
                b.RiskLevel,
                b.Status,
                b.CreatedAt,
                b.ResolvedAt
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> CreateBug([FromBody] CreateBugDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var task = await _db.Tasks.FindAsync(dto.TaskId);
        if (task == null) return NotFound("Task not found");

        var bug = new Bug
        {
            TaskId = dto.TaskId,
            ReportedBy = userId.Value,
            BugDescription = dto.BugDescription,
            RiskLevel = dto.RiskLevel,
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bugs.Add(bug);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetBugs), new { taskId = dto.TaskId }, new
        {
            bug.BugId,
            bug.TaskId,
            bug.ReportedBy,
            bug.BugDescription,
            bug.RiskLevel,
            bug.Status,
            bug.CreatedAt
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBug(int id, [FromBody] UpdateBugDto dto)
    {
        var bug = await _db.Bugs.FindAsync(id);
        if (bug == null) return NotFound();

        bug.BugDescription = dto.BugDescription;
        bug.RiskLevel = dto.RiskLevel;
        bug.Status = dto.Status;
        if (dto.Status == 2) bug.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new
        {
            bug.BugId,
            bug.BugDescription,
            bug.RiskLevel,
            bug.Status,
            bug.ResolvedAt
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBug(int id)
    {
        var bug = await _db.Bugs.FindAsync(id);
        if (bug == null) return NotFound();

        _db.Bugs.Remove(bug);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateBugDto(int TaskId, string BugDescription, byte RiskLevel);
public record UpdateBugDto(string BugDescription, byte RiskLevel, byte Status);
