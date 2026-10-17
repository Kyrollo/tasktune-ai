using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/projects")]
    public class ProjectsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetAll(int page = 1, int pageSize = 25, byte? status = null)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var query = db.Projects.Include(p => p.Manager).Include(p => p.Tasks)
                .Where(p => p.CompanyId == companyId);

            if (status.HasValue) query = query.Where(p => p.Status == status.Value);

            var total = await query.CountAsync();
            var projects = await query.OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PagedResultDto<ProjectResponseDto>
            {
                Items = projects.Select(MapProject).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Get(int id)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var p = await db.Projects.Include(x => x.Manager).Include(x => x.Tasks)
                .FirstOrDefaultAsync(x => x.ProjectId == id && x.CompanyId == companyId);
            if (p == null) return NotFound();
            return Ok(MapProject(p));
        }

        [HttpPost, Route("")]
        [JwtAuthorize(RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Create(CreateProjectDto dto)
        {
            if (dto == null) return BadRequest();
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var project = new Project
            {
                CompanyId = companyId,
                ProjectName = dto.ProjectName,
                Description = dto.Description,
                ManagerId = dto.ManagerId,
                Status = dto.Status,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return Created($"api/projects/{project.ProjectId}", MapProject(project));
        }

        [HttpPut, Route("{id:int}")]
        [JwtAuthorize(RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Update(int id, UpdateProjectDto dto)
        {
            if (dto == null) return BadRequest();
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectId == id && p.CompanyId == companyId);
            if (project == null) return NotFound();

            if (!string.IsNullOrEmpty(dto.ProjectName)) project.ProjectName = dto.ProjectName;
            if (dto.Description != null) project.Description = dto.Description;
            if (dto.ManagerId.HasValue) project.ManagerId = dto.ManagerId.Value;
            if (dto.Status.HasValue) project.Status = dto.Status.Value;
            if (dto.StartDate.HasValue) project.StartDate = dto.StartDate.Value;
            if (dto.EndDate.HasValue) project.EndDate = dto.EndDate.Value;
            project.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Ok(MapProject(project));
        }

        [HttpDelete, Route("{id:int}")]
        [JwtAuthorize(RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectId == id && p.CompanyId == companyId);
            if (project == null) return NotFound();
            db.Projects.Remove(project);
            await db.SaveChangesAsync();
            return StatusCode(System.Net.HttpStatusCode.NoContent);
        }

        [HttpGet, Route("{id:int}/tasks")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetProjectTasks(int id)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var tasks = await db.Tasks
                .Include(t => t.AssignedToUser).Include(t => t.CreatedByUser).Include(t => t.Project)
                .Where(t => t.ProjectId == id && t.Project.CompanyId == companyId)
                .OrderBy(t => t.DueDate).ToListAsync();

            return Ok(tasks.Select(TasksController.MapTaskStatic).ToList());
        }

        private ProjectResponseDto MapProject(Project p)
        {
            return new ProjectResponseDto
            {
                ProjectId = p.ProjectId,
                CompanyId = p.CompanyId,
                ProjectName = p.ProjectName,
                Description = p.Description,
                ManagerId = p.ManagerId,
                ManagerName = p.Manager != null ? p.Manager.FullName ?? (p.Manager.FirstName + " " + p.Manager.LastName) : null,
                Status = p.Status,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                TaskCount = p.Tasks?.Count ?? 0,
                CompletedTaskCount = p.Tasks?.Count(t => t.Status == 2) ?? 0,
                CreatedAt = p.CreatedAt
            };
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}