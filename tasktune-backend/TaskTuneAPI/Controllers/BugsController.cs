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
    [RoutePrefix("api")]
    public class BugsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpPost, Route("tasks/{taskId:int}/bugs")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Create(int taskId, CreateBugDto dto)
        {
            if (dto == null) return BadRequest("Request body is required");

            var task = await db.Tasks.FindAsync(taskId);
            if (task == null) return NotFound();

            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var bug = new Bug
            {
                TaskId = taskId,
                ReportedBy = userId,
                BugDescription = dto.BugDescription,
                RiskLevel = dto.RiskLevel,
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            db.Bugs.Add(bug);
            await db.SaveChangesAsync();

            return Created($"api/bugs/{bug.BugId}", MapBug(bug));
        }

        [HttpGet, Route("tasks/{taskId:int}/bugs")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetByTask(int taskId)
        {
            var bugs = await db.Bugs
                .Include(b => b.ReportedByUser)
                .Include(b => b.Task)
                .Where(b => b.TaskId == taskId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Ok(bugs.Select(MapBug).ToList());
        }

        [HttpPut, Route("bugs/{id:int}")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Update(int id, [FromBody] UpdateBugDto dto)
        {
            if (dto == null) return BadRequest();

            var bug = await db.Bugs.FindAsync(id);
            if (bug == null) return NotFound();

            if (dto.Status.HasValue) bug.Status = dto.Status.Value;
            if (dto.RiskLevel.HasValue) bug.RiskLevel = dto.RiskLevel.Value;
            if (!string.IsNullOrEmpty(dto.BugDescription)) bug.BugDescription = dto.BugDescription;

            if (dto.Status.HasValue && dto.Status.Value == 2)
                bug.ResolvedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Ok(MapBug(bug));
        }

        private BugResponseDto MapBug(Bug b)
        {
            return new BugResponseDto
            {
                BugId = b.BugId,
                TaskId = b.TaskId,
                TaskName = b.Task?.TaskName,
                ReportedByName = b.ReportedByUser != null
                    ? (b.ReportedByUser.FullName ?? (b.ReportedByUser.FirstName + " " + b.ReportedByUser.LastName))
                    : null,
                BugDescription = b.BugDescription,
                RiskLevel = b.RiskLevel,
                Status = b.Status,
                CreatedAt = b.CreatedAt,
                ResolvedAt = b.ResolvedAt
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // DTO for update — add to CommonDtos.cs or keep here
    public class UpdateBugDto
    {
        public byte? Status { get; set; }
        public byte? RiskLevel { get; set; }
        public string BugDescription { get; set; }
    }
}