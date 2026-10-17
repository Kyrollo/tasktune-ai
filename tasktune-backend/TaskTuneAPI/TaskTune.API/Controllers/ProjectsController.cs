using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public ProjectsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized("Invalid token");

        var companyId = GetCurrentCompanyId();

        var query = _db.Projects.AsQueryable();

        if (companyId.HasValue)
            query = query.Where(p => p.CompanyId == companyId.Value);
        else
            query = query.Where(p => p.ManagerId == userId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.ProjectId,
                p.ProjectName,
                p.Description,
                p.Status,
                p.ManagerId,
                ManagerName = p.Manager != null ? p.Manager.FullName : "",
                p.StartDate,
                p.EndDate,
                p.CreatedAt,
                TaskCount = p.Tasks.Count(),
                CompletedTaskCount = p.Tasks.Count(t => t.Status == 2)
            })
            .ToListAsync();

        return Ok(new { items, totalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProject(int id)
    {
        var companyId = GetCurrentCompanyId();
        var project = await _db.Projects
            .Include(p => p.Manager)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.AssignedToNavigation)
            .FirstOrDefaultAsync(p => p.ProjectId == id && (!companyId.HasValue || p.CompanyId == companyId.Value));

        if (project == null) return NotFound();

        return Ok(new
        {
            project.ProjectId,
            project.ProjectName,
            project.Description,
            project.Status,
            project.CompanyId,
            project.ManagerId,
            ManagerName = project.Manager?.FullName ?? "",
            project.StartDate,
            project.EndDate,
            project.CreatedAt,
            Tasks = project.Tasks
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.Priority)
                .Select(t => new
            {
                t.TaskId,
                t.TaskName,
                t.Description,
                t.Status,
                t.Priority,
                t.AssignedTo,
                AssignedToName = t.AssignedToNavigation != null
                    ? (t.AssignedToNavigation.FullName ?? (t.AssignedToNavigation.FirstName + " " + t.AssignedToNavigation.LastName))
                    : null,
                t.DueDate,
                t.StartDate,
                t.CompletionPercentage
            })
        });
    }

    [HttpPost]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var project = new Project
        {
            CompanyId = companyId.Value,
            ProjectName = dto.ProjectName,
            Description = dto.Description,
            ManagerId = dto.ManagerId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetProject), new { id = project.ProjectId }, new
        {
            project.ProjectId,
            project.ProjectName,
            project.CompanyId,
            project.ManagerId,
            project.StartDate,
            project.EndDate
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] CreateProjectDto dto)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();

        project.ProjectName = dto.ProjectName;
        project.Description = dto.Description;
        project.ManagerId = dto.ManagerId;
        project.StartDate = dto.StartDate;
        project.EndDate = dto.EndDate;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            project.ProjectId,
            project.ProjectName,
            project.ManagerId,
            project.StartDate,
            project.EndDate
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateProjectDto(
    string ProjectName, string? Description,
    int ManagerId, DateOnly StartDate, DateOnly EndDate
);
