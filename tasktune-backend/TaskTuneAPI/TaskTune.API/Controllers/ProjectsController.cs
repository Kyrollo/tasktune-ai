using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
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
    public async Task<IActionResult> GetProjects([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        var role = GetCurrentRole();
        if (userId == null || companyId == null || role == null) return Unauthorized();
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "Page must be positive and pageSize must be between 1 and 100." });

        var query = _db.Projects.AsNoTracking()
            .Where(p => p.CompanyId == companyId.Value);

        if (role.Equals("Employee", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.ManagerId == userId.Value ||
                p.Tasks.Any(t => t.AssignedTo == userId.Value || t.CreatedBy == userId.Value));
        }
        else if (role.Equals("Supervisor", StringComparison.OrdinalIgnoreCase))
        {
            var departmentId = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == userId.Value && u.CompanyId == companyId.Value)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync();
            query = query.Where(p => p.ManagerId == userId.Value ||
                p.Tasks.Any(t => t.AssignedTo == userId.Value || t.CreatedBy == userId.Value ||
                                 (departmentId.HasValue && t.AssignedToNavigation.DepartmentId == departmentId.Value)));
        }
        else if (!new[] { "Manager", "HR", "CEO", "Admin" }.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

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

        return Ok(new { items, totalCount = total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProject(int id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        if (!await CanViewProjectAsync(_db, id)) return NotFound();

        var project = await _db.Projects.AsNoTracking()
            .Include(p => p.Manager)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.AssignedToNavigation)
            .FirstOrDefaultAsync(p => p.ProjectId == id && p.CompanyId == companyId.Value);
        if (project == null) return NotFound();

        var visibleTasks = new List<Models.Task>();
        foreach (var task in project.Tasks)
        {
            if (await CanViewTaskAsync(_db, task.TaskId))
                visibleTasks.Add(task);
        }

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
            Tasks = visibleTasks
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
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.ProjectName) || dto.ProjectName.Length > 255)
            return BadRequest(new { message = "Project name is required and must not exceed 255 characters." });
        if (dto.EndDate < dto.StartDate)
            return BadRequest(new { message = "Project end date cannot be earlier than its start date." });
        if (!await _db.Users.AnyAsync(u => u.UserId == dto.ManagerId && u.CompanyId == companyId.Value && u.IsActive))
            return BadRequest(new { message = "Project manager must be an active user in this company." });

        var project = new Project
        {
            CompanyId = companyId.Value,
            ProjectName = dto.ProjectName.Trim(),
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
        await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "CREATE", "Project", project.ProjectId,
            after: new { project.ProjectName, project.ManagerId, project.StartDate, project.EndDate });

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
        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.ProjectId == id && p.CompanyId == companyId.Value);
        if (project == null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.ProjectName) || dto.ProjectName.Length > 255 || dto.EndDate < dto.StartDate)
            return BadRequest(new { message = "Project name and date range are invalid." });
        if (!await _db.Users.AnyAsync(u => u.UserId == dto.ManagerId && u.CompanyId == companyId.Value && u.IsActive))
            return BadRequest(new { message = "Project manager must be an active user in this company." });

        var before = new { project.ProjectName, project.ManagerId, project.StartDate, project.EndDate };
        project.ProjectName = dto.ProjectName.Trim();
        project.Description = dto.Description;
        project.ManagerId = dto.ManagerId;
        project.StartDate = dto.StartDate;
        project.EndDate = dto.EndDate;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "UPDATE", "Project", id,
            before: before,
            after: new { project.ProjectName, project.ManagerId, project.StartDate, project.EndDate });

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
    [Authorize(Roles = "Manager,CEO,Admin")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.ProjectId == id && p.CompanyId == companyId.Value);
        if (project == null) return NotFound();

        var before = new { project.ProjectName, project.ManagerId };
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "DELETE", "Project", id, before: before);
        return NoContent();
    }
}

public record CreateProjectDto(
    string ProjectName, string? Description,
    int ManagerId, DateOnly StartDate, DateOnly EndDate
);
