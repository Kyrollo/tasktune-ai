using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskTune.Application.DTOs.Tasks;
using TaskTune.Application.Interfaces;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : BaseApiController
{
    private readonly ITaskService _taskService;
    private readonly TaskTuneDbContext _db;

    public TasksController(ITaskService taskService, TaskTuneDbContext db)
    {
        _taskService = taskService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _taskService.GetTasksAsync(userId.Value, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTask(int id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null) return NotFound();
        return Ok(task);
    }

    [HttpPost]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var task = await _taskService.CreateAsync(dto, userId.Value);
        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            userId,
            action: "CREATE",
            resourceType: "Task",
            resourceId: task.TaskId,
            after: task);
        return CreatedAtAction(nameof(GetTask), new { id = task.TaskId }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] CreateTaskDto dto)
    {
        var userId = GetCurrentUserId();
        var before = await _taskService.GetByIdAsync(id);
        var task = await _taskService.UpdateAsync(id, dto);
        if (task == null) return NotFound();
        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            userId,
            action: "UPDATE",
            resourceType: "Task",
            resourceId: id,
            before: before,
            after: task);
        return Ok(task);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var userId = GetCurrentUserId();
        var before = await _taskService.GetByIdAsync(id);
        if (!await _taskService.DeleteAsync(id)) return NotFound();
        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            userId,
            action: "DELETE",
            resourceType: "Task",
            resourceId: id,
            before: before,
            after: null);
        return NoContent();
    }

    [HttpGet("{id:int}/progress")]
    public async Task<IActionResult> GetProgress(int id)
    {
        var progress = await _taskService.GetProgressAsync(id);
        return Ok(progress);
    }

    [HttpPost("{id:int}/progress")]
    public async Task<IActionResult> SubmitProgress(int id, [FromBody] SubmitProgressDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        if (!await _taskService.SubmitProgressAsync(id, dto, userId.Value)) return NotFound();
        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            userId,
            action: "CREATE",
            resourceType: "Task_Progress",
            resourceId: id,
            after: new
            {
                taskId = id,
                dto.CompletionPercentage,
                dto.Notes,
                dto.Blockers
            });
        return Ok(new { message = "Progress submitted." });
    }

    [HttpGet("{id:int}/guidance")]
    public async Task<IActionResult> GetGuidance(int id)
    {
        var latest = await _db.AIActionsLogs
            .Where(a => a.ActionType == 1 && a.TaskId == id)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => a.OutputData)
            .FirstOrDefaultAsync();

        return Ok(ParseOutput(latest) ?? new { guidance_text = "", suggested_subtasks = Array.Empty<string>() });
    }

    [HttpPost("{id:int}/guidance/generate")]
    public async Task<IActionResult> GenerateGuidance(int id)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });
        var task = await _db.Tasks
            .Include(t => t.Project)
            .Include(t => t.Subtasks)
            .Include(t => t.TaskDependencyTasks)
                .ThenInclude(td => td.DependsOnTask)
            .FirstOrDefaultAsync(t => t.TaskId == id);
        if (task == null) return NotFound();

        var aiConfig = await AIConfigurationStore.LoadAsync(_db, companyId.Value);
        var agentConfig = aiConfig.GetAgent("task_guidance");
        if (!aiConfig.Enabled || !aiConfig.Features.TaskGuidance || !agentConfig.Enabled)
        {
            return StatusCode(503, new { message = "Task guidance is disabled in AI Control Panel." });
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var payload = new
        {
            task_name = task.TaskName,
            description = task.Description ?? "",
            subtasks = task.Subtasks
                .OrderBy(s => s.OrderIndex)
                .Select(s => new
                {
                    subtask_name = s.SubtaskName,
                    description = s.Description ?? "",
                    status = s.Status
                }),
            definition_of_done = task.DefinitionOfDone ?? "",
            due_date = task.DueDate.ToString("yyyy-MM-dd"),
            dependencies = task.TaskDependencyTasks
                .Select(td => td.DependsOnTask != null ? td.DependsOnTask.TaskName : string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList(),
            industry = task.Project?.ProjectName ?? "General",
            user_role = GetCurrentRole() ?? "Employee",
            completion_percentage = task.CompletionPercentage,
            agent_config = new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                api_key = agentConfig.ApiKey,
                temperature = agentConfig.Temperature,
                max_tokens = agentConfig.MaxTokens
            }
        };

        try
        {
            var agentBaseUrl = string.IsNullOrWhiteSpace(agentConfig.BaseUrl)
                ? (Environment.GetEnvironmentVariable("AGENT_GUIDANCE_URL") ?? "http://localhost:5050")
                : agentConfig.BaseUrl;
            var endpoint = string.IsNullOrWhiteSpace(agentConfig.Endpoint) ? "/api/guidance" : agentConfig.Endpoint;
            var response = await http.PostAsJsonAsync($"{agentBaseUrl.TrimEnd('/')}{endpoint}", payload);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return StatusCode(502, new { message = "AI agent returned an error.", detail = err });
            }
            var result = await response.Content.ReadFromJsonAsync<object>() ?? new { guidance_text = "", suggested_subtasks = Array.Empty<string>() };
            if (userId != null)
            {
                _db.AIActionsLogs.Add(new AIActionsLog
                {
                    ActionType = 1,
                    TaskId = id,
                    UserId = userId.Value,
                    InputData = JsonSerializer.Serialize(payload),
                    OutputData = JsonSerializer.Serialize(result),
                    ModelVersion = agentConfig.Model,
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                await AuditLogWriter.WriteAsync(
                    _db,
                    HttpContext,
                    userId,
                    action: "GENERATE",
                    resourceType: "Task_Guidance",
                    resourceId: id,
                    after: result);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Failed to reach AI guidance agent.", detail = ex.Message });
        }
    }

    [HttpPost("ai-recommendation")]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> GenerateTaskRecommendation([FromBody] TaskRecommendationRequest request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var aiConfig = await AIConfigurationStore.LoadAsync(_db, companyId.Value);
        var agentConfig = aiConfig.GetAgent("task_guidance");
        if (!aiConfig.Enabled || !aiConfig.Features.TaskGuidance || !agentConfig.Enabled)
        {
            return StatusCode(503, new { message = "Task guidance is disabled in AI Control Panel." });
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var payload = new
        {
            task_name = request.TaskName ?? "",
            description = request.Description ?? "",
            subtasks = request.Subtasks ?? [],
            definition_of_done = request.DefinitionOfDone ?? request.Description ?? "",
            due_date = request.DueDate ?? "",
            dependencies = request.Dependencies ?? [],
            industry = request.ProjectName ?? "General",
            user_role = GetCurrentRole() ?? "Supervisor",
            completion_percentage = request.CompletionPercentage ?? 0,
            agent_config = new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                api_key = agentConfig.ApiKey,
                temperature = agentConfig.Temperature,
                max_tokens = agentConfig.MaxTokens
            }
        };

        try
        {
            var agentBaseUrl = string.IsNullOrWhiteSpace(agentConfig.BaseUrl)
                ? (Environment.GetEnvironmentVariable("AGENT_GUIDANCE_URL") ?? "http://localhost:5050")
                : agentConfig.BaseUrl;
            var endpoint = string.IsNullOrWhiteSpace(agentConfig.Endpoint) ? "/api/guidance" : agentConfig.Endpoint;
            var response = await http.PostAsJsonAsync($"{agentBaseUrl.TrimEnd('/')}{endpoint}", payload);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return StatusCode(502, new { message = "AI agent returned an error.", detail = err });
            }
            var result = await response.Content.ReadFromJsonAsync<object>();
            if (userId != null && result != null)
            {
                _db.AIActionsLogs.Add(new AIActionsLog
                {
                    ActionType = 1,
                    UserId = userId.Value,
                    InputData = JsonSerializer.Serialize(payload),
                    OutputData = JsonSerializer.Serialize(result),
                    ModelVersion = agentConfig.Model,
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Failed to reach AI guidance agent.", detail = ex.Message });
        }
    }

    [HttpPost("{id:int}/progress/ai-analyze")]
    public async Task<IActionResult> AnalyzeProgress(int id, [FromBody] AnalyzeProgressRequest? request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });
        var task = await _db.Tasks
            .Include(t => t.TaskProgresses)
            .FirstOrDefaultAsync(t => t.TaskId == id);
        if (task == null) return NotFound();

        var aiConfig = await AIConfigurationStore.LoadAsync(_db, companyId.Value);
        var agentConfig = aiConfig.GetAgent("progress_tracking");
        if (!aiConfig.Enabled || !aiConfig.Features.RiskDetection || !agentConfig.Enabled)
        {
            return StatusCode(503, new { message = "Progress tracking is disabled in AI Control Panel." });
        }

        // Build progress updates list from history
        var progressUpdates = task.TaskProgresses
            .OrderBy(p => p.SubmittedAt)
            .Select(p => (object)new
            {
                submitted_at = p.SubmittedAt.ToString("o"),
                completion_percentage = p.CompletionPercentage,
                notes = p.Notes ?? "",
                blockers = p.Blockers ?? ""
            }).ToList();

        if (request != null && (request.CompletionPercentage.HasValue || !string.IsNullOrWhiteSpace(request.Notes) || !string.IsNullOrWhiteSpace(request.Blockers)))
        {
            progressUpdates.Add(new
            {
                submitted_at = DateTime.UtcNow.ToString("o"),
                completion_percentage = request.CompletionPercentage ?? task.CompletionPercentage,
                notes = request.Notes ?? "",
                blockers = request.Blockers ?? ""
            });
        }

        var recentBlockers = task.TaskProgresses
            .OrderByDescending(p => p.SubmittedAt)
            .Where(p => !string.IsNullOrWhiteSpace(p.Blockers))
            .Take(3)
            .Select(p => p.Blockers!)
            .ToList();

        if (!string.IsNullOrWhiteSpace(request?.Blockers))
        {
            recentBlockers.Insert(0, request.Blockers.Trim());
        }

        var taskPayload = new
        {
            task_id = task.TaskId.ToString(),
            task_title = task.TaskName ?? "",
            task_description = task.Description ?? "",
            current_completion_percentage = request?.CompletionPercentage ?? task.CompletionPercentage,
            start_date = task.StartDate.ToString("yyyy-MM-dd"),
            due_date = task.DueDate.ToString("yyyy-MM-dd"),
            progress_updates = progressUpdates,
            current_blockers = recentBlockers,
            user_performance_history = new List<object>(),
            agent_config = new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                api_key = agentConfig.ApiKey,
                temperature = agentConfig.Temperature,
                max_tokens = agentConfig.MaxTokens
            }
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        try
        {
            var agentBaseUrl = string.IsNullOrWhiteSpace(agentConfig.BaseUrl)
                ? (Environment.GetEnvironmentVariable("AGENT_PROGRESS_URL") ?? "http://localhost:8003")
                : agentConfig.BaseUrl;
            var endpoint = string.IsNullOrWhiteSpace(agentConfig.Endpoint) ? "/api/ai/progress-tracking" : agentConfig.Endpoint;
            var response = await http.PostAsJsonAsync($"{agentBaseUrl.TrimEnd('/')}{endpoint}", taskPayload);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return StatusCode(502, new { message = "AI progress agent returned an error.", detail = err });
            }
            var result = await response.Content.ReadFromJsonAsync<object>();
            if (userId != null && result != null)
            {
                _db.AIActionsLogs.Add(new AIActionsLog
                {
                    ActionType = 2,
                    TaskId = id,
                    UserId = userId.Value,
                    InputData = JsonSerializer.Serialize(taskPayload),
                    OutputData = JsonSerializer.Serialize(result),
                    ModelVersion = agentConfig.Model,
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                await AuditLogWriter.WriteAsync(
                    _db,
                    HttpContext,
                    userId,
                    action: "GENERATE",
                    resourceType: "Task_Progress_AI",
                    resourceId: id,
                    after: result);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Failed to reach AI progress agent.", detail = ex.Message });
        }
    }

    [HttpGet("{id:int}/progress/ai-analysis")]
    public async Task<IActionResult> GetLatestProgressAnalysis(int id)
    {
        var latest = await _db.AIActionsLogs
            .Where(a => a.ActionType == 2 && a.TaskId == id)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => a.OutputData)
            .FirstOrDefaultAsync();

        return Ok(ParseOutput(latest) ?? new { });
    }

    private static object? ParseOutput(string? outputData)
    {
        if (string.IsNullOrWhiteSpace(outputData))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(outputData);
        }
        catch
        {
            return outputData;
        }
    }
}

public record TaskRecommendationRequest(
    string? TaskName,
    string? Description,
    string? DueDate,
    string? DefinitionOfDone,
    string? ProjectName,
    List<object>? Subtasks,
    List<string>? Dependencies,
    int? CompletionPercentage);

public record AnalyzeProgressRequest(
    int? CompletionPercentage,
    string? Notes,
    string? Blockers);
