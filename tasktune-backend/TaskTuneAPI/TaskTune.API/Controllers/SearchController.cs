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
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2 || q.Length > 100)
            return BadRequest(new { message = "Query must contain between 2 and 100 characters." });

        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        limit = Math.Clamp(limit, 1, 50);

        var currentUser = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);
        if (currentUser == null) return Unauthorized();

        var term = q.Trim().ToLowerInvariant();
        var pattern = $"%{term}%";
        var role = currentUser.Role;
        var broadDirectoryAccess = role is 3 or 4 or 5;

        var usersQuery = _db.Users.Where(u => u.IsActive);
        if (role == 0)
            usersQuery = usersQuery.Where(u => u.UserId == userId.Value);
        else if (role is 1 or 2)
            usersQuery = usersQuery.Where(u => u.UserId == userId.Value ||
                (currentUser.DepartmentId.HasValue && u.DepartmentId == currentUser.DepartmentId.Value));

        var users = await usersQuery
            .Where(u => EF.Functions.Like(u.FirstName.ToLower(), pattern) ||
                        EF.Functions.Like(u.LastName.ToLower(), pattern) ||
                        (broadDirectoryAccess && EF.Functions.Like(u.Email.ToLower(), pattern)))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).Take(limit)
            .Select(u => new
            {
                type = "user",
                id = u.UserId,
                name = u.FullName ?? (u.FirstName + " " + u.LastName),
                email = broadDirectoryAccess ? u.Email : null
            }).ToListAsync();

        var tasksQuery = _db.Tasks.AsQueryable();
        tasksQuery = role switch
        {
            0 => tasksQuery.Where(t => t.AssignedTo == userId.Value || t.CreatedBy == userId.Value),
            1 or 2 when currentUser.DepartmentId.HasValue => tasksQuery.Where(t =>
                t.Project.ManagerId == userId.Value || t.CreatedBy == userId.Value ||
                t.AssignedTo == userId.Value ||
                t.AssignedToNavigation.DepartmentId == currentUser.DepartmentId.Value),
            1 or 2 => tasksQuery.Where(t => t.Project.ManagerId == userId.Value ||
                t.CreatedBy == userId.Value || t.AssignedTo == userId.Value),
            _ => tasksQuery.Where(t => t.Project.CompanyId == companyId.Value)
        };

        var tasks = await tasksQuery
            .Where(t => EF.Functions.Like(t.TaskName.ToLower(), pattern) ||
                        (t.Description != null && EF.Functions.Like(t.Description.ToLower(), pattern)))
            .OrderBy(t => t.TaskName).Take(limit)
            .Select(t => new
            {
                type = "task", id = t.TaskId, name = t.TaskName, description = t.Description
            }).ToListAsync();

        var projectsQuery = _db.Projects.AsQueryable();
        if (role == 0)
            projectsQuery = projectsQuery.Where(p => p.ManagerId == userId.Value ||
                p.Tasks.Any(t => t.AssignedTo == userId.Value || t.CreatedBy == userId.Value));
        else if (role is 1 or 2)
            projectsQuery = projectsQuery.Where(p => p.ManagerId == userId.Value ||
                p.Tasks.Any(t => t.CreatedBy == userId.Value || t.AssignedTo == userId.Value ||
                    (currentUser.DepartmentId.HasValue &&
                     t.AssignedToNavigation.DepartmentId == currentUser.DepartmentId.Value)));
        else
            projectsQuery = projectsQuery.Where(p => p.CompanyId == companyId.Value);

        var projects = await projectsQuery
            .Where(p => EF.Functions.Like(p.ProjectName.ToLower(), pattern) ||
                        (p.Description != null && EF.Functions.Like(p.Description.ToLower(), pattern)))
            .OrderBy(p => p.ProjectName).Take(limit)
            .Select(p => new
            {
                type = "project", id = p.ProjectId, name = p.ProjectName, description = p.Description
            }).ToListAsync();

        return Ok(new { users, tasks, projects });
    }
}
