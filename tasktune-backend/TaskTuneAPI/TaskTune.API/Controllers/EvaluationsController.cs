using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Application.DTOs.Evaluations;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EvaluationsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public EvaluationsController(TaskTuneDbContext db) => _db = db;

    [HttpPost]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> CreateEvaluation([FromBody] CreateEvaluationDto dto)
    {
        var evaluatorId = GetCurrentUserId();
        if (evaluatorId == null) return Unauthorized();

        var eval = new Evaluation
        {
            TaskId = dto.TaskId,
            EvaluatedUserId = dto.EvaluatedUserId,
            EvaluatorId = evaluatorId.Value,
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
        _db.Evaluations.Add(eval);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEvaluation), new { id = eval.EvaluationId }, new
        {
            eval.EvaluationId,
            eval.TaskId,
            eval.EvaluatedUserId,
            eval.EvaluatorId,
            eval.OverallScore
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEvaluation(int id)
    {
        var eval = await _db.Evaluations
            .Include(e => e.EvaluatedUser)
            .Include(e => e.Evaluator)
            .FirstOrDefaultAsync(e => e.EvaluationId == id);
        if (eval == null) return NotFound();

        return Ok(new
        {
            eval.EvaluationId,
            eval.TaskId,
            eval.EvaluatedUserId,
            EvaluatedUserName = eval.EvaluatedUser?.FullName ?? "",
            eval.EvaluatorId,
            EvaluatorName = eval.Evaluator?.FullName ?? "",
            eval.OverallScore,
            eval.QualityScore,
            eval.TimelinessScore,
            eval.CollaborationScore,
            eval.RiskLevel,
            eval.ManualFeedback,
            eval.AiGeneratedFeedback,
            eval.Status,
            eval.SubmittedAt,
            eval.CreatedAt
        });
    }

    [HttpGet("user/{userId:int}")]
    public async Task<IActionResult> GetUserEvaluations(int userId)
    {
        var evals = await _db.Evaluations
            .Include(e => e.Task)
            .Where(e => e.EvaluatedUserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.EvaluationId,
                e.TaskId,
                TaskName = e.Task.TaskName,
                e.EvaluatorId,
                e.OverallScore,
                e.QualityScore,
                e.TimelinessScore,
                e.CollaborationScore,
                e.RiskLevel,
                e.ManualFeedback,
                e.AiGeneratedFeedback,
                e.Status,
                e.CreatedAt
            })
            .ToListAsync();
        return Ok(evals);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyEvaluations()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var role = GetCurrentRole();
        if (role is "Supervisor" or "Manager" or "CEO" or "Admin")
        {
            return Ok(await GetReviewerEvaluationsAsync(userId.Value, role));
        }

        var evals = await _db.Evaluations
            .Include(e => e.Task)
            .Where(e => e.EvaluatedUserId == userId.Value)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                type = "employee",
                e.EvaluationId,
                e.TaskId,
                TaskName = e.Task.TaskName,
                e.EvaluatorId,
                e.OverallScore,
                e.QualityScore,
                e.TimelinessScore,
                e.CollaborationScore,
                e.RiskLevel,
                e.ManualFeedback,
                e.AiGeneratedFeedback,
                e.Status,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(evals);
    }

    private async Task<List<object>> GetReviewerEvaluationsAsync(int reviewerId, string? role)
    {
        var reviewer = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == reviewerId);
        if (reviewer == null) return [];

        var tasksQuery = _db.Tasks
            .Include(t => t.AssignedToNavigation)
                .ThenInclude(u => u.Department)
            .Include(t => t.Project)
            .AsQueryable();

        tasksQuery = role switch
        {
            "Supervisor" when reviewer.DepartmentId.HasValue => tasksQuery.Where(t =>
                t.CreatedBy == reviewerId ||
                t.AssignedToNavigation.DepartmentId == reviewer.DepartmentId),
            "Supervisor" => tasksQuery.Where(t => t.CreatedBy == reviewerId || t.AssignedTo == reviewerId),
            "Manager" => tasksQuery.Where(t => t.Project.ManagerId == reviewerId || t.CreatedBy == reviewerId || t.Project.CompanyId == reviewer.CompanyId),
            "CEO" or "Admin" => tasksQuery.Where(t => t.Project.CompanyId == reviewer.CompanyId),
            _ => tasksQuery.Where(t => t.AssignedTo == reviewerId || t.CreatedBy == reviewerId),
        };

        var candidateTasks = await tasksQuery
            .Where(t => t.CompletionPercentage >= 100 || t.Status == 2 || t.Status == 3)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new
            {
                t.TaskId,
                t.TaskName,
                t.ProjectId,
                ProjectName = t.Project.ProjectName,
                t.AssignedTo,
                EmployeeName = t.AssignedToNavigation.FullName ?? (t.AssignedToNavigation.FirstName + " " + t.AssignedToNavigation.LastName),
                DepartmentName = t.AssignedToNavigation.Department != null ? t.AssignedToNavigation.Department.DepartmentName : "Unassigned",
                t.CompletionPercentage,
                t.UpdatedAt
            })
            .ToListAsync();

        var taskIds = candidateTasks.Select(t => t.TaskId).ToList();
        var existingEvaluations = await _db.Evaluations
            .Where(e => e.EvaluatorId == reviewerId && taskIds.Contains(e.TaskId))
            .Select(e => new
            {
                e.EvaluationId,
                e.TaskId,
                e.Task.TaskName,
                e.EvaluatedUserId,
                EmployeeName = e.EvaluatedUser.FullName ?? (e.EvaluatedUser.FirstName + " " + e.EvaluatedUser.LastName),
                DepartmentName = e.EvaluatedUser.Department != null ? e.EvaluatedUser.Department.DepartmentName : "Unassigned",
                e.OverallScore,
                e.QualityScore,
                e.TimelinessScore,
                e.CollaborationScore,
                e.ManualFeedback,
                e.AiGeneratedFeedback,
                e.Status,
                e.CreatedAt
            })
            .ToListAsync();

        var reviewedByTaskId = existingEvaluations.ToDictionary(e => e.TaskId, e => e);
        var pending = candidateTasks
            .Where(t => !reviewedByTaskId.ContainsKey(t.TaskId))
            .Select(t => new
            {
                createdAt = t.UpdatedAt,
                item = (object)new
                {
                    type = "reviewer",
                    reviewState = "Pending Review",
                    needsReview = true,
                    evaluationId = (int?)null,
                    taskId = t.TaskId,
                    taskName = t.TaskName,
                    evaluatedUserId = t.AssignedTo,
                    employeeName = t.EmployeeName,
                    departmentName = t.DepartmentName,
                    overallScore = (decimal?)null,
                    qualityScore = (decimal?)null,
                    timelinessScore = (decimal?)null,
                    collaborationScore = (decimal?)null,
                    manualFeedback = (string?)null,
                    aiGeneratedFeedback = (string?)null,
                    createdAt = t.UpdatedAt,
                    completionPercentage = t.CompletionPercentage
                }
            });

        var reviewed = existingEvaluations.Select(e => new
        {
            createdAt = e.CreatedAt,
            item = (object)new
            {
                type = "reviewer",
                reviewState = "Reviewed",
                needsReview = false,
                e.EvaluationId,
                e.TaskId,
                taskName = e.TaskName,
                e.EvaluatedUserId,
                employeeName = e.EmployeeName,
                departmentName = e.DepartmentName,
                e.OverallScore,
                e.QualityScore,
                e.TimelinessScore,
                e.CollaborationScore,
                e.ManualFeedback,
                e.AiGeneratedFeedback,
                e.CreatedAt
            }
        });

        return pending.Concat(reviewed)
            .OrderByDescending(entry => entry.createdAt)
            .Select(entry => entry.item)
            .ToList();
    }
}
