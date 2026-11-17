using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Application.Interfaces;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BugsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly ITaskService _taskService;

    public BugsController(TaskTuneDbContext db, ITaskService taskService)
    {
        _db = db;
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<IActionResult> GetBugs([FromQuery] int? taskId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var role = GetCurrentRole();
        if (taskId.HasValue)
        {
            if (!await _taskService.CanAccessTaskAsync(taskId.Value, userId.Value)) return NotFound();
        }
        else if (role is "Employee" or "Supervisor")
        {
            return BadRequest(new { message = "A taskId is required for your access scope." });
        }

        var query = _db.Bugs.AsQueryable();
        if (taskId.HasValue) query = query.Where(b => b.TaskId == taskId.Value);
        var items = await query.OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.BugId, b.TaskId, b.ReportedBy,
                ReportedByName = b.ReportedByNavigation.FirstName + " " + b.ReportedByNavigation.LastName,
                b.BugDescription, b.RiskLevel, b.Status, b.CreatedAt, b.ResolvedAt
            }).ToListAsync();
        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> CreateBug([FromBody] CreateBugDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        if (!await _taskService.CanAccessTaskAsync(dto.TaskId, userId.Value)) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.BugDescription) || dto.BugDescription.Length > 4000 || dto.RiskLevel > 5)
            return BadRequest(new { message = "Bug details or risk level are invalid." });

        var bug = new Bug
        {
            TaskId = dto.TaskId, ReportedBy = userId.Value, BugDescription = dto.BugDescription.Trim(),
            RiskLevel = dto.RiskLevel, Status = 0, CreatedAt = DateTime.UtcNow
        };
        _db.Bugs.Add(bug);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetBugs), new { taskId = dto.TaskId }, new
        {
            bug.BugId, bug.TaskId, bug.ReportedBy, bug.BugDescription, bug.RiskLevel, bug.Status, bug.CreatedAt
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> UpdateBug(int id, [FromBody] UpdateBugDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var bug = await _db.Bugs.FirstOrDefaultAsync(b => b.BugId == id);
        if (bug == null || !await _taskService.CanAccessTaskAsync(bug.TaskId, userId.Value)) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.BugDescription) || dto.BugDescription.Length > 4000 || dto.RiskLevel > 5 || dto.Status > 3)
            return BadRequest(new { message = "Bug details, risk level or status are invalid." });

        bug.BugDescription = dto.BugDescription.Trim();
        bug.RiskLevel = dto.RiskLevel;
        bug.Status = dto.Status;
        bug.ResolvedAt = dto.Status == 2 ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        return Ok(new { bug.BugId, bug.BugDescription, bug.RiskLevel, bug.Status, bug.ResolvedAt });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> DeleteBug(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var bug = await _db.Bugs.FirstOrDefaultAsync(b => b.BugId == id);
        if (bug == null || !await _taskService.CanAccessTaskAsync(bug.TaskId, userId.Value)) return NotFound();
        _db.Bugs.Remove(bug);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateBugDto(int TaskId, string BugDescription, byte RiskLevel);
public record UpdateBugDto(string BugDescription, byte RiskLevel, byte Status);
