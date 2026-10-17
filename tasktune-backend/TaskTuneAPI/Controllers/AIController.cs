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
    [RoutePrefix("api/ai")]
    public class AIController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpPost, Route("task-guidance")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> TaskGuidance([FromBody] AITaskGuidanceRequestDto dto)
        {
            if (dto == null || dto.TaskId <= 0) return BadRequest("TaskId is required");

            var task = await db.Tasks
                .Include(t => t.Subtasks)
                .Include(t => t.Dependencies.Select(d => d.DependsOnTask))
                .FirstOrDefaultAsync(t => t.TaskId == dto.TaskId);

            if (task == null) return NotFound();

            // Placeholder AI response — replace with actual AI API call
            var guidance = new
            {
                guidanceText = $"Focus on completing '{task.TaskName}' by breaking it into smaller steps. You are currently at {task.CompletionPercentage}% completion.",
                suggestedSteps = task.Subtasks?.Where(s => s.Status != 2).Select(s => s.SubtaskName).ToList(),
                estimatedTime = "2-4 hours",
                riskLevel = task.DueDate < DateTime.UtcNow ? "High" : task.DueDate < DateTime.UtcNow.AddDays(3) ? "Medium" : "Low"
            };

            // Log the AI action
            db.AIActionsLog.Add(new AIActionLog
            {
                ActionType = 0, // Guidance
                TaskId = task.TaskId,
                UserId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId(),
                InputData = Newtonsoft.Json.JsonConvert.SerializeObject(new { task.TaskId, task.TaskName, task.CompletionPercentage }),
                OutputData = Newtonsoft.Json.JsonConvert.SerializeObject(guidance),
                ModelVersion = "placeholder-v1",
                Timestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            return Ok(guidance);
        }

        [HttpPost, Route("evaluation-feedback")]
        [JwtAuthorize(RoleNames.Supervisor, RoleNames.Manager, RoleNames.CEO)]
        public async Task<IHttpActionResult> EvaluationFeedback([FromBody] AIEvaluationFeedbackRequestDto dto)
        {
            if (dto == null || dto.EvaluationId <= 0) return BadRequest("EvaluationId is required");

            var eval = await db.Evaluations
                .Include(e => e.Task)
                .Include(e => e.EvaluatedUser)
                .FirstOrDefaultAsync(e => e.EvaluationId == dto.EvaluationId);

            if (eval == null) return NotFound();

            // Placeholder AI feedback
            var feedback = new
            {
                aiFeedback = $"Based on the scores (Overall: {eval.OverallScore}, Quality: {eval.QualityScore}), the employee shows strong performance in task '{eval.Task?.TaskName}'.",
                strengths = new[] { "Consistent delivery", "Quality focus" },
                improvementAreas = eval.TimelinessScore < 80 ? new[] { "Time management", "Deadline adherence" } : new[] { "Continue current pace" },
                recommendedTraining = eval.CollaborationScore < 80 ? new[] { "Team collaboration workshop" } : new string[0]
            };

            // Store AI feedback on the evaluation
            eval.AiGeneratedFeedback = Newtonsoft.Json.JsonConvert.SerializeObject(feedback);
            await db.SaveChangesAsync();

            // Log
            db.AIActionsLog.Add(new AIActionLog
            {
                ActionType = 1,
                TaskId = eval.TaskId,
                UserId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId(),
                InputData = Newtonsoft.Json.JsonConvert.SerializeObject(new { eval.EvaluationId, eval.OverallScore }),
                OutputData = Newtonsoft.Json.JsonConvert.SerializeObject(feedback),
                ModelVersion = "placeholder-v1",
                Timestamp = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            return Ok(feedback);
        }

        [HttpGet, Route("config")]
        [JwtAuthorize(RoleNames.Admin, RoleNames.CEO)]
        public async Task<IHttpActionResult> GetConfig()
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var company = await db.Companies.FindAsync(companyId);
            if (company == null) return NotFound();

            return Ok(new
            {
                company.CompanyId,
                settingsJson = company.SettingsJson
            });
        }

        [HttpPut, Route("config")]
        [JwtAuthorize(RoleNames.Admin, RoleNames.CEO)]
        public async Task<IHttpActionResult> UpdateConfig([FromBody] UpdateAIConfigDto dto)
        {
            if (dto == null) return BadRequest();

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var company = await db.Companies.FindAsync(companyId);
            if (company == null) return NotFound();

            company.SettingsJson = dto.SettingsJson;
            company.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(new MessageResponseDto { Message = "AI configuration updated" });
        }

        [HttpGet, Route("logs")]
        [JwtAuthorize(RoleNames.Admin)]
        public async Task<IHttpActionResult> GetLogs(int page = 1, int pageSize = 25, byte? actionType = null)
        {
            var query = db.AIActionsLog.AsQueryable();

            if (actionType.HasValue)
                query = query.Where(a => a.ActionType == actionType.Value);

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PagedResultDto<AIActionLogResponseDto>
            {
                Items = items.Select(a => new AIActionLogResponseDto
                {
                    AiLogId = a.AiLogId,
                    ActionType = a.ActionType,
                    TaskId = a.TaskId,
                    UserId = a.UserId,
                    InputData = a.InputData,
                    OutputData = a.OutputData,
                    ModelVersion = a.ModelVersion,
                    Timestamp = a.Timestamp
                }).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // Request DTOs
    public class AITaskGuidanceRequestDto
    {
        public int TaskId { get; set; }
    }

    public class AIEvaluationFeedbackRequestDto
    {
        public int EvaluationId { get; set; }
    }

    public class UpdateAIConfigDto
    {
        public string SettingsJson { get; set; }
    }

    // Response DTO
    public class AIActionLogResponseDto
    {
        public int AiLogId { get; set; }
        public byte ActionType { get; set; }
        public int? TaskId { get; set; }
        public int? UserId { get; set; }
        public string InputData { get; set; }
        public string OutputData { get; set; }
        public string ModelVersion { get; set; }
        public DateTime Timestamp { get; set; }
    }
}