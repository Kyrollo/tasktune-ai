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
    [RoutePrefix("api/evaluations")]
    public class EvaluationsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpPost, Route("")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Create(CreateEvaluationDto dto)
        {
            if (dto == null) return BadRequest();
            var evaluatorId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var eval = new Evaluation
            {
                TaskId = dto.TaskId,
                EvaluatedUserId = dto.EvaluatedUserId,
                EvaluatorId = evaluatorId,
                OverallScore = dto.OverallScore,
                QualityScore = dto.QualityScore,
                TimelinessScore = dto.TimelinessScore,
                CollaborationScore = dto.CollaborationScore,
                RiskLevel = dto.RiskLevel,
                ManualFeedback = dto.ManualFeedback,
                Status = 1,
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.Evaluations.Add(eval);
            await db.SaveChangesAsync();

            return Created($"api/evaluations/{eval.EvaluationId}", new { eval.EvaluationId });
        }

        [HttpGet, Route("task/{taskId:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetByTask(int taskId, int page = 1, int pageSize = 25)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var query = db.Evaluations
                .Include(e => e.Task).Include(e => e.Task.Project).Include(e => e.EvaluatedUser).Include(e => e.Evaluator)
                .Where(e => e.TaskId == taskId && e.Task.Project.CompanyId == companyId);

            var total = await query.CountAsync();
            var evals = await query.OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PagedResultDto<EvaluationResponseDto>
            {
                Items = evals.Select(MapEval).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet, Route("user/{userId:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetByUser(int userId, int page = 1, int pageSize = 25)
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var query = db.Evaluations
                .Include(e => e.Task).Include(e => e.EvaluatedUser).Include(e => e.Evaluator)
                .Where(e => e.EvaluatedUserId == userId && e.EvaluatedUser.CompanyId == companyId);

            var total = await query.CountAsync();
            var evals = await query.OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PagedResultDto<EvaluationResponseDto>
            {
                Items = evals.Select(MapEval).ToList(),
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
            var eval = await db.Evaluations
                .Include(e => e.Task).Include(e => e.EvaluatedUser).Include(e => e.Evaluator)
                .FirstOrDefaultAsync(e => e.EvaluationId == id);
            if (eval == null) return NotFound();
            return Ok(MapEval(eval));
        }

        [HttpPut, Route("{id:int}")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO)]
        public async Task<IHttpActionResult> Update(int id, UpdateEvaluationDto dto)
        {
            var eval = await db.Evaluations.FindAsync(id);
            if (eval == null) return NotFound();

            if (dto.OverallScore.HasValue) eval.OverallScore = dto.OverallScore.Value;
            if (dto.QualityScore.HasValue) eval.QualityScore = dto.QualityScore.Value;
            if (dto.TimelinessScore.HasValue) eval.TimelinessScore = dto.TimelinessScore.Value;
            if (dto.CollaborationScore.HasValue) eval.CollaborationScore = dto.CollaborationScore.Value;
            if (dto.RiskLevel.HasValue) eval.RiskLevel = dto.RiskLevel.Value;
            if (dto.ManualFeedback != null) eval.ManualFeedback = dto.ManualFeedback;
            if (dto.Status.HasValue) eval.Status = dto.Status.Value;

            await db.SaveChangesAsync();
            return Ok(MapEval(eval));
        }

        private EvaluationResponseDto MapEval(Evaluation e)
        {
            return new EvaluationResponseDto
            {
                EvaluationId = e.EvaluationId,
                TaskId = e.TaskId,
                TaskName = e.Task?.TaskName,
                EvaluatedUser = e.EvaluatedUser != null ? new UserSummaryDto { UserId = e.EvaluatedUser.UserId, FullName = e.EvaluatedUser.FullName ?? (e.EvaluatedUser.FirstName + " " + e.EvaluatedUser.LastName), Email = e.EvaluatedUser.Email, Role = e.EvaluatedUser.Role, RoleName = RoleNames.GetName(e.EvaluatedUser.Role) } : null,
                Evaluator = e.Evaluator != null ? new UserSummaryDto { UserId = e.Evaluator.UserId, FullName = e.Evaluator.FullName ?? (e.Evaluator.FirstName + " " + e.Evaluator.LastName), Email = e.Evaluator.Email, Role = e.Evaluator.Role, RoleName = RoleNames.GetName(e.Evaluator.Role) } : null,
                OverallScore = e.OverallScore,
                QualityScore = e.QualityScore,
                TimelinessScore = e.TimelinessScore,
                CollaborationScore = e.CollaborationScore,
                RiskLevel = e.RiskLevel,
                ManualFeedback = e.ManualFeedback,
                AiGeneratedFeedback = e.AiGeneratedFeedback,
                Status = e.Status,
                SubmittedAt = e.SubmittedAt,
                CreatedAt = e.CreatedAt
            };
        }

        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}