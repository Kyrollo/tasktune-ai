using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskTune.Application.Interfaces;
using TaskTune.Application.DTOs;
using TaskTune.Application.DTOs.Tasks;
using TaskTune.Infrastructure.Models;

namespace TaskTune.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly TaskTuneDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TaskService(TaskTuneDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<TaskResponseDto>> GetTasksAsync(int userId, int page, int pageSize)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            return new PagedResultDto<TaskResponseDto>([], 0, page, pageSize, 0);
        }

        var query = _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedToNavigation)
            .AsQueryable();

        query = ApplyTaskScope(query, user);

        var totalCount = await query.CountAsync();
        var rawTasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var aiMap = await GetAiRecommendationMapAsync(rawTasks.Select(t => t.TaskId).ToList());
        var tasks = rawTasks.Select(t => MapTaskResponse(t, aiMap.GetValueOrDefault(t.TaskId))).ToList();

        return new PagedResultDto<TaskResponseDto>(
            tasks, totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize));
    }

    public async Task<TaskResponseDto?> GetByIdAsync(int taskId)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedToNavigation)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);

        if (task == null)
            return null;

        var aiRecommendation = await GetLatestAiRecommendationAsync(task.TaskId);
        return MapTaskResponse(task, aiRecommendation);
    }

    public async Task<TaskResponseDto> CreateAsync(CreateTaskDto dto, int createdBy)
    {
        var task = new Models.Task
        {
            ProjectId = dto.ProjectId,
            TaskName = dto.TaskName,
            Description = dto.Description,
            Priority = dto.Priority,
            Status = 0,
            AssignedTo = dto.AssignedTo,
            CreatedBy = createdBy,
            StartDate = dto.StartDate,
            DueDate = dto.DueDate,
            SubmitMethod = dto.SubmitMethod,
            DefinitionOfDone = dto.DefinitionOfDone,
            CompletionPercentage = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        if (dto.Subtasks?.Count > 0)
        {
            var subtasks = dto.Subtasks
                .Where(s => !string.IsNullOrWhiteSpace(s.SubtaskName))
                .Select((subtask, index) => new Subtask
                {
                    TaskId = task.TaskId,
                    SubtaskName = subtask.SubtaskName.Trim(),
                    Description = subtask.Description,
                    Status = 0,
                    OrderIndex = subtask.OrderIndex ?? index,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (subtasks.Count > 0)
            {
                _context.Subtasks.AddRange(subtasks);
            }
        }

        if (dto.AiRecommendation != null)
        {
            _context.AIActionsLogs.Add(new AIActionsLog
            {
                ActionType = 1,
                TaskId = task.TaskId,
                UserId = createdBy,
                InputData = JsonSerializer.Serialize(new
                {
                    dto.TaskName,
                    dto.Description,
                    dto.DefinitionOfDone,
                    dto.DueDate,
                    dto.Priority,
                    dto.AssignedTo
                }, JsonOptions),
                OutputData = JsonSerializer.Serialize(dto.AiRecommendation, JsonOptions),
                ModelVersion = "agent2_task_guidance",
                Timestamp = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        var result = await GetByIdAsync(task.TaskId);
        return result!;
    }

    public async Task<TaskResponseDto?> UpdateAsync(int taskId, CreateTaskDto dto)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
            return null;

        task.ProjectId = dto.ProjectId;
        task.TaskName = dto.TaskName;
        task.Description = dto.Description;
        task.Priority = dto.Priority;
        task.AssignedTo = dto.AssignedTo;
        task.StartDate = dto.StartDate;
        task.DueDate = dto.DueDate;
        task.SubmitMethod = dto.SubmitMethod;
        task.DefinitionOfDone = dto.DefinitionOfDone;
        task.UpdatedAt = DateTime.UtcNow;

        if (dto.AiRecommendation != null)
        {
            _context.AIActionsLogs.Add(new AIActionsLog
            {
                ActionType = 1,
                TaskId = taskId,
                UserId = task.CreatedBy,
                InputData = JsonSerializer.Serialize(new
                {
                    dto.TaskName,
                    dto.Description,
                    dto.DefinitionOfDone,
                    dto.DueDate,
                    dto.Priority,
                    dto.AssignedTo
                }, JsonOptions),
                OutputData = JsonSerializer.Serialize(dto.AiRecommendation, JsonOptions),
                ModelVersion = "agent2_task_guidance",
                Timestamp = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return await GetByIdAsync(taskId);
    }

    public async Task<bool> DeleteAsync(int taskId)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
            return false;

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SubmitProgressAsync(int taskId, SubmitProgressDto dto, int userId)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
            return false;

        if (task.AssignedTo != userId)
            return false;

        var progress = new TaskProgress
        {
            TaskId = taskId,
            CompletionPercentage = dto.CompletionPercentage,
            Notes = dto.Notes,
            Blockers = dto.Blockers,
            SubmittedBy = userId,
            SubmittedAt = DateTime.UtcNow
        };

        _context.TaskProgresses.Add(progress);
        task.CompletionPercentage = dto.CompletionPercentage;
        task.UpdatedAt = DateTime.UtcNow;

        if (dto.CompletionPercentage >= 100)
            task.Status = 2; // Completed (was incorrectly 1)

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<object> GetProgressAsync(int taskId)
    {
        var items = await _context.TaskProgresses
            .Where(tp => tp.TaskId == taskId)
            .OrderByDescending(tp => tp.SubmittedAt)
            .Select(tp => new
            {
                tp.ProgressId,
                tp.TaskId,
                tp.SubmittedBy,
                SubmittedByName = tp.SubmittedByNavigation.FullName ?? (tp.SubmittedByNavigation.FirstName + " " + tp.SubmittedByNavigation.LastName),
                tp.CompletionPercentage,
                tp.Notes,
                tp.Blockers,
                tp.SubmittedAt
            })
            .ToListAsync();
        return items;
    }

    private IQueryable<Models.Task> ApplyTaskScope(IQueryable<Models.Task> query, User user)
    {
        return user.Role switch
        {
            0 => query.Where(t => t.AssignedTo == user.UserId || t.CreatedBy == user.UserId),
            1 when user.DepartmentId.HasValue => query.Where(t =>
                t.CreatedBy == user.UserId ||
                t.AssignedTo == user.UserId ||
                t.AssignedToNavigation.DepartmentId == user.DepartmentId),
            1 => query.Where(t => t.CreatedBy == user.UserId || t.AssignedTo == user.UserId),
            2 or 3 or 4 or 5 => query.Where(t => t.Project.CompanyId == user.CompanyId),
            _ => query.Where(t => t.AssignedTo == user.UserId || t.CreatedBy == user.UserId),
        };
    }

    private async Task<Dictionary<int, object?>> GetAiRecommendationMapAsync(List<int> taskIds)
    {
        if (taskIds.Count == 0)
        {
            return [];
        }

        var logs = await _context.AIActionsLogs
            .Where(a => a.ActionType == 1 && a.TaskId.HasValue && taskIds.Contains(a.TaskId.Value))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        return logs
            .GroupBy(a => a.TaskId!.Value)
            .ToDictionary(g => g.Key, g => DeserializeOutput(g.First().OutputData));
    }

    private async Task<object?> GetLatestAiRecommendationAsync(int taskId)
    {
        var log = await _context.AIActionsLogs
            .Where(a => a.ActionType == 1 && a.TaskId == taskId)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        return DeserializeOutput(log?.OutputData);
    }

    private TaskResponseDto MapTaskResponse(Models.Task task, object? aiRecommendation)
    {
        return new TaskResponseDto(
            task.TaskId,
            task.ProjectId,
            task.Project?.ProjectName ?? "",
            task.TaskName,
            task.Description,
            task.Priority,
            task.Status,
            task.AssignedTo,
            task.AssignedToNavigation?.FullName ?? (task.AssignedToNavigation?.FirstName + " " + task.AssignedToNavigation?.LastName).Trim(),
            task.CreatedBy,
            task.StartDate,
            task.DueDate,
            task.CompletionPercentage,
            task.CreatedAt,
            task.UpdatedAt,
            task.SubmitMethod,
            task.DefinitionOfDone,
            aiRecommendation);
    }

    private static object? DeserializeOutput(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(raw, JsonOptions);
        }
        catch
        {
            return raw;
        }
    }
}
