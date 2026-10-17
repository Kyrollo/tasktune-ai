using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/chatbot")]
[Authorize]
public class ChatbotController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ChatbotController(TaskTuneDbContext db) => _db = db;

    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var threads = await _db.ChatThreads
            .Where(t => t.UserId == userId.Value)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new
            {
                t.ThreadId,
                t.Title,
                t.RelatedTaskId,
                t.RelatedProjectId,
                t.Status,
                t.CreatedAt,
                t.UpdatedAt,
                LastMessage = t.ChatMessages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new
        {
            items = threads.Select(t => new
            {
                t.ThreadId,
                id = t.ThreadId,
                t.Title,
                t.RelatedTaskId,
                t.RelatedProjectId,
                t.Status,
                t.CreatedAt,
                t.UpdatedAt,
                t.LastMessage
            })
        });
    }

    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread([FromBody] CreateChatThreadRequest? request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var thread = new ChatThread
        {
            UserId = userId.Value,
            RelatedTaskId = request?.RelatedTaskId,
            RelatedProjectId = request?.RelatedProjectId,
            Title = string.IsNullOrWhiteSpace(request?.Title) ? "New Chat" : request!.Title.Trim(),
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ChatThreads.Add(thread);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            thread.ThreadId,
            id = thread.ThreadId,
            thread.Title,
            thread.RelatedTaskId,
            thread.RelatedProjectId,
            thread.CreatedAt,
            thread.UpdatedAt
        });
    }

    [HttpGet("threads/{threadId:int}")]
    public async Task<IActionResult> GetThread(int threadId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var thread = await _db.ChatThreads
            .Include(t => t.ChatMessages.Where(m => !m.IsDeleted).OrderBy(m => m.SentAt))
            .FirstOrDefaultAsync(t => t.ThreadId == threadId && t.UserId == userId.Value);

        if (thread == null) return NotFound();

        return Ok(new
        {
            thread.ThreadId,
            id = thread.ThreadId,
            thread.Title,
            thread.RelatedTaskId,
            thread.RelatedProjectId,
            thread.CreatedAt,
            thread.UpdatedAt,
            messages = thread.ChatMessages
                .Where(m => !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.MessageId,
                    id = m.MessageId,
                    role = m.Role == 0 ? "user" : "assistant",
                    content = m.Content,
                    m.SentAt
                })
        });
    }

    [HttpPost("threads/{threadId:int}/messages")]
    public async Task<IActionResult> SendMessage(int threadId, [FromBody] SendChatMessageRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Content)) return BadRequest(new { message = "Message content is required." });

        var thread = await _db.ChatThreads.FirstOrDefaultAsync(t => t.ThreadId == threadId && t.UserId == userId.Value);
        if (thread == null) return NotFound();

        var user = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.Position)
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);
        if (user == null) return Unauthorized();

        var userMessage = new ChatMessage
        {
            ThreadId = threadId,
            Role = 0,
            Content = request.Content.Trim(),
            ModelVersion = "tasktune_ai_assistant",
            SentAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(userMessage);

        if (thread.Title == "New Chat")
        {
            thread.Title = BuildThreadTitle(request.Content);
        }

        var execution = await BuildAssistantReplyAsync(user, thread, request.Content.Trim());
        var assistantMessage = new ChatMessage
        {
            ThreadId = threadId,
            Role = 1,
            Content = execution.Reply,
            ModelVersion = execution.ModelVersion,
            SentAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(assistantMessage);

        thread.UpdatedAt = DateTime.UtcNow;

        _db.AIActionsLogs.Add(new AIActionsLog
        {
            ActionType = 4,
            UserId = user.UserId,
            TaskId = thread.RelatedTaskId,
            InputData = JsonSerializer.Serialize(new
            {
                message = request.Content.Trim(),
                generatedSql = execution.GeneratedSql,
                executedSql = execution.ExecutedSql,
                resultCount = execution.ResultCount
            }, JsonOptions),
            OutputData = execution.Reply,
            ModelVersion = execution.ModelVersion,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            threadId = thread.ThreadId,
            threadTitle = thread.Title,
            userMessage = new { id = userMessage.MessageId, role = "user", content = userMessage.Content, sentAt = userMessage.SentAt },
            assistantMessage = new { id = assistantMessage.MessageId, role = "assistant", content = assistantMessage.Content, sentAt = assistantMessage.SentAt }
        });
    }

    private async Task<ChatExecutionResult> BuildAssistantReplyAsync(User user, ChatThread thread, string message)
    {
        var scope = await BuildScopeAsync(user, thread);
        var config = await AIConfigurationStore.LoadAsync(_db, user.CompanyId);
        var agentConfig = config.GetAgent("chat_assistant");

        if (config.Enabled && config.Features.ChatAssistant && agentConfig.Enabled)
        {
            var sqlReply = await TryGetSqlBackedReplyAsync(user, thread, message, scope, agentConfig);
            if (sqlReply != null)
            {
                return sqlReply;
            }

            var directReply = await TryGetAgentResponseAsync(BuildAgentPayload(user, thread, message, scope, null, agentConfig), agentConfig);
            if (!string.IsNullOrWhiteSpace(directReply?.Reply))
            {
                return new ChatExecutionResult
                {
                    Reply = directReply.Reply!,
                    ModelVersion = agentConfig.Model
                };
            }
        }

        return new ChatExecutionResult
        {
            Reply = BuildFallbackReply(user, thread, message, scope),
            ModelVersion = "tasktune_ai_assistant_fallback"
        };
    }

    private async Task<ChatExecutionResult?> TryGetSqlBackedReplyAsync(
        User user,
        ChatThread thread,
        string message,
        ChatScope scope,
        AIAgentConfig agentConfig)
    {
        try
        {
            await ChatbotSqlUtility.EnsureViewsAsync(_db);

            var roleName = GetRoleName(user.Role);
            var roleRules = ChatbotSqlUtility.GetRoleSqlRules(
                roleName,
                user.UserId,
                user.CompanyId,
                user.DepartmentId,
                user.FullName ?? (user.FirstName + " " + user.LastName));

            var planPayload = BuildAgentPayload(user, thread, message, scope, new
            {
                phase = "plan",
                viewSchema = ChatbotSqlUtility.ViewSchemaDescription,
                roleRules,
                executedSql = (string?)null,
                rows = Array.Empty<object>()
            }, agentConfig);

            var plan = await TryGetAgentResponseAsync(planPayload, agentConfig);
            if (plan == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(plan.SqlQuery))
            {
                if (!string.IsNullOrWhiteSpace(plan.Reply))
                {
                    return new ChatExecutionResult
                    {
                        Reply = plan.Reply!,
                        ModelVersion = agentConfig.Model
                    };
                }

                return null;
            }

            var (rows, error, executedSql) = await ChatbotSqlUtility.ExecuteScopedQueryAsync(
                _db,
                plan.SqlQuery,
                user.CompanyId,
                roleName,
                user.UserId,
                user.DepartmentId);

            var answerPayload = BuildAgentPayload(user, thread, message, scope, new
            {
                phase = "answer",
                viewSchema = ChatbotSqlUtility.ViewSchemaDescription,
                roleRules,
                generatedSql = plan.SqlQuery,
                executedSql,
                rows,
                sqlError = error
            }, agentConfig);

            var answer = await TryGetAgentResponseAsync(answerPayload, agentConfig);
            if (!string.IsNullOrWhiteSpace(answer?.Reply))
            {
                return new ChatExecutionResult
                {
                    Reply = answer.Reply!,
                    GeneratedSql = plan.SqlQuery,
                    ExecutedSql = executedSql,
                    ResultCount = rows.Count,
                    ModelVersion = agentConfig.Model
                };
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<ChatbotAgentResponse?> TryGetAgentResponseAsync(object payload, AIAgentConfig config)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "http://localhost:8001" : config.BaseUrl.TrimEnd('/');
            var endpoint = string.IsNullOrWhiteSpace(config.Endpoint) ? "/api/chat" : config.Endpoint;
            var response = await http.PostAsJsonAsync($"{baseUrl}{endpoint}", payload);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ChatbotAgentResponse>();
        }
        catch
        {
            return null;
        }
    }

    private object BuildAgentPayload(User user, ChatThread thread, string message, ChatScope scope, object? sqlContext, AIAgentConfig agentConfig)
    {
        var history = _db.ChatMessages
            .Where(m => m.ThreadId == thread.ThreadId && !m.IsDeleted)
            .OrderByDescending(m => m.SentAt)
            .Take(12)
            .AsEnumerable()
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                role = m.Role == 0 ? "user" : "assistant",
                content = m.Content,
                sent_at = m.SentAt
            })
            .ToList();

        var focusedTask = thread.RelatedTaskId.HasValue
            ? scope.Tasks.FirstOrDefault(t => t.TaskId == thread.RelatedTaskId.Value)
            : null;

        var focusedProject = thread.RelatedProjectId.HasValue
            ? scope.Projects.FirstOrDefault(p => p.ProjectId == thread.RelatedProjectId.Value)
            : null;

        return new
        {
            message,
            current_date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            user = new
            {
                id = user.UserId,
                name = user.FullName ?? (user.FirstName + " " + user.LastName),
                email = user.Email,
                role = GetRoleName(user.Role),
                department = user.Department != null ? user.Department.DepartmentName : "Unassigned",
                position = user.Position != null ? user.Position.PositionTitle : "N/A"
            },
            thread = new
            {
                id = thread.ThreadId,
                title = thread.Title,
                related_task_id = thread.RelatedTaskId,
                related_project_id = thread.RelatedProjectId
            },
            history,
            scope,
            focus = new
            {
                task = focusedTask,
                project = focusedProject
            },
            available_data = new
            {
                task_count = scope.Tasks.Count,
                project_count = scope.Projects.Count,
                employee_count = scope.EmployeeDirectory.Count,
                evaluation_count = scope.Evaluations.Count,
                report_count = scope.Reports.Count,
                notification_count = scope.Notifications.Count
            },
            sql_context = sqlContext,
            agent_config = new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                api_key = agentConfig.ApiKey,
                temperature = agentConfig.Temperature,
                max_tokens = agentConfig.MaxTokens
            }
        };
    }

    private static string BuildFallbackReply(User user, ChatThread thread, string message, ChatScope scope)
    {
        var lower = message.ToLowerInvariant();

        if (lower.Contains("hello") || lower.Contains("hi") || lower.Contains("hey"))
        {
            return $"Hello {user.FirstName}, I can answer using the data available to your {GetRoleName(user.Role)} access level. Ask about tasks, projects, reports, employees, evaluations, or notifications inside your scope.";
        }

        if (thread.RelatedTaskId.HasValue && (lower.Contains("this task") || lower.Contains("task status") || lower.Contains("help")))
        {
            var relatedTask = scope.Tasks.FirstOrDefault(t => t.TaskId == thread.RelatedTaskId.Value);
            if (relatedTask != null)
            {
                return $"Task: {relatedTask.TaskName}\nStatus: {relatedTask.StatusName}\nProgress: {relatedTask.CompletionPercentage}%\nDue: {relatedTask.DueDate:yyyy-MM-dd}\nProject: {relatedTask.ProjectName}";
            }
        }

        if (lower.Contains("task"))
        {
            if (GetRoleName(user.Role) == "HR")
            {
                return "Your HR access does not include individual task details. I can still help with employee, department, evaluation, and report information.";
            }

            var matchedTask = scope.Tasks.FirstOrDefault(t => lower.Contains(t.TaskName.ToLowerInvariant()));
            if (matchedTask != null)
            {
                return $"Task: {matchedTask.TaskName}\nStatus: {matchedTask.StatusName}\nProgress: {matchedTask.CompletionPercentage}%\nAssigned to: {matchedTask.AssignedToName}\nProject: {matchedTask.ProjectName}\nDue: {matchedTask.DueDate:yyyy-MM-dd}";
            }
        }

        if (lower.Contains("report") && scope.Reports.Any())
        {
            var recent = scope.Reports.Take(5).Select(r => $"{r.Title} ({r.GeneratedAt:yyyy-MM-dd})");
            return $"Recent reports in your scope:\n- {string.Join("\n- ", recent)}";
        }

        return $"I can answer from the data your {GetRoleName(user.Role)} role can access. Try asking about tasks, evaluations, notifications, employees, projects, or recent reports.";
    }

    private async Task<ChatScope> BuildScopeAsync(User user, ChatThread thread)
    {
        var companyId = user.CompanyId;

        var taskQuery = _db.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedToNavigation)
            .AsQueryable();

        var projectQuery = _db.Projects
            .Include(p => p.Manager)
            .Include(p => p.Tasks)
            .Where(p => p.CompanyId == companyId)
            .AsQueryable();

        var userQuery = _db.Users
            .Include(u => u.Department)
            .Include(u => u.Position)
            .Where(u => u.CompanyId == companyId && u.IsActive)
            .AsQueryable();

        var evaluationQuery = _db.Evaluations
            .Include(e => e.EvaluatedUser)
                .ThenInclude(u => u.Department)
            .Where(e => e.EvaluatedUser.CompanyId == companyId)
            .AsQueryable();

        var notificationQuery = _db.Notifications
            .Include(n => n.RelatedTask)
            .Where(n => n.UserId == user.UserId)
            .AsQueryable();

        if (user.Role == 0)
        {
            taskQuery = taskQuery.Where(t => t.AssignedTo == user.UserId);
            projectQuery = projectQuery.Where(p => p.Tasks.Any(t => t.AssignedTo == user.UserId));
            userQuery = userQuery.Where(u => u.UserId == user.UserId);
            evaluationQuery = evaluationQuery.Where(e => e.EvaluatedUserId == user.UserId);
        }
        else if (user.Role == 1)
        {
            taskQuery = taskQuery.Where(t => t.CreatedBy == user.UserId || t.AssignedToNavigation.DepartmentId == user.DepartmentId);
            projectQuery = projectQuery.Where(p => p.ManagerId == user.UserId || p.Tasks.Any(t => t.CreatedBy == user.UserId || t.AssignedToNavigation.DepartmentId == user.DepartmentId));
            userQuery = userQuery.Where(u => u.DepartmentId == user.DepartmentId);
            evaluationQuery = evaluationQuery.Where(e => e.EvaluatedUser.DepartmentId == user.DepartmentId);
        }
        else if (user.Role == 3)
        {
            taskQuery = taskQuery.Where(t => false);
            projectQuery = projectQuery.Where(p => false);
        }

        var tasks = await taskQuery
            .OrderByDescending(t => t.UpdatedAt)
            .Take(100)
            .Select(t => new TaskScopeItem(
                t.TaskId,
                t.TaskName,
                t.Project.ProjectName,
                t.AssignedToNavigation.FullName ?? (t.AssignedToNavigation.FirstName + " " + t.AssignedToNavigation.LastName),
                t.Status,
                GetTaskStatusName(t.Status),
                t.CompletionPercentage,
                t.DueDate))
            .ToListAsync();

        var projects = await projectQuery
            .OrderByDescending(p => p.UpdatedAt)
            .Take(50)
            .Select(p => new ProjectScopeItem(
                p.ProjectId,
                p.ProjectName,
                GetProjectStatusName(p.Status),
                p.Manager != null ? (p.Manager.FullName ?? (p.Manager.FirstName + " " + p.Manager.LastName)) : "Unknown",
                p.Tasks.Count,
                p.Tasks.Count(t => t.Status == 2)))
            .ToListAsync();

        var employees = await userQuery
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Take(100)
            .Select(u => new EmployeeScopeItem(
                u.UserId,
                u.FullName ?? (u.FirstName + " " + u.LastName),
                GetRoleName(u.Role),
                u.Department != null ? u.Department.DepartmentName : "Unassigned",
                u.Position != null ? u.Position.PositionTitle : "N/A"))
            .ToListAsync();

        var evaluations = await evaluationQuery
            .OrderByDescending(e => e.CreatedAt)
            .Take(100)
            .Select(e => new EvaluationScopeItem(
                e.EvaluatedUser.FullName ?? (e.EvaluatedUser.FirstName + " " + e.EvaluatedUser.LastName),
                (double)e.OverallScore,
                e.CreatedAt))
            .ToListAsync();

        var reports = await _db.Reports
            .Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.GeneratedAt)
            .Take(user.Role == 0 ? 0 : 10)
            .Select(r => new ReportScopeItem(
                r.ReportId,
                r.MetadataJson,
                r.GeneratedAt))
            .ToListAsync();

        var notifications = await notificationQuery
            .OrderByDescending(n => n.SentAt)
            .Take(50)
            .Select(n => new NotificationScopeItem(
                n.NotificationId,
                n.Title,
                n.Message,
                n.RelatedTask != null ? n.RelatedTask.TaskName : null,
                n.IsRead,
                n.SentAt))
            .ToListAsync();

        return new ChatScope(
            tasks,
            projects,
            employees,
            evaluations,
            reports.Select(r => new ChatReportItem(
                r.ReportId,
                TryGetReportTitle(r.MetadataJson) ?? $"{GetReportTypeNameFromMetadata(r.MetadataJson)} Report",
                r.GeneratedAt)).ToList(),
            notifications);
    }

    private static string BuildThreadTitle(string content)
    {
        var clean = content.Trim();
        return clean.Length <= 48 ? clean : clean[..45] + "...";
    }

    private static string GetTaskStatusName(byte status) => status switch
    {
        1 => "In Progress",
        2 => "Completed",
        3 => "In Review",
        _ => "Not Started"
    };

    private static string GetProjectStatusName(byte status) => status switch
    {
        1 => "Active",
        2 => "Completed",
        _ => "Planning"
    };

    private static string GetRoleName(byte role) => role switch
    {
        1 => "Supervisor",
        2 => "Manager",
        3 => "HR",
        4 => "CEO",
        5 => "Admin",
        _ => "Employee"
    };

    private static string? TryGetReportTitle(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }
        }
        catch { }

        return null;
    }

    private static string GetReportTypeNameFromMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return "AI";
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty("report_type", out var type))
            {
                return type.GetString() ?? "AI";
            }
        }
        catch { }

        return "AI";
    }
}

public class CreateChatThreadRequest
{
    public string? Title { get; set; }
    public int? RelatedTaskId { get; set; }
    public int? RelatedProjectId { get; set; }
}

public class SendChatMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class ChatbotAgentResponse
{
    public string? Reply { get; set; }
    public string? SqlQuery { get; set; }
    public string? ModeUsed { get; set; }
}

public class ChatExecutionResult
{
    public string Reply { get; set; } = "";
    public string? GeneratedSql { get; set; }
    public string? ExecutedSql { get; set; }
    public int ResultCount { get; set; }
    public string ModelVersion { get; set; } = "tasktune_ai_assistant";
}

public record TaskScopeItem(int TaskId, string TaskName, string ProjectName, string AssignedToName, byte Status, string StatusName, int CompletionPercentage, DateOnly DueDate);
public record ProjectScopeItem(int ProjectId, string ProjectName, string StatusName, string ManagerName, int TaskCount, int CompletedTaskCount);
public record EmployeeScopeItem(int UserId, string FullName, string RoleName, string DepartmentName, string PositionTitle);
public record EvaluationScopeItem(string EmployeeName, double OverallScore, DateTime CreatedAt);
public record ReportScopeItem(int ReportId, string? MetadataJson, DateTime GeneratedAt);
public record ChatReportItem(int ReportId, string Title, DateTime GeneratedAt);
public record NotificationScopeItem(int NotificationId, string Title, string Message, string? RelatedTaskName, bool IsRead, DateTime SentAt);
public record ChatScope(
    List<TaskScopeItem> Tasks,
    List<ProjectScopeItem> Projects,
    List<EmployeeScopeItem> EmployeeDirectory,
    List<EvaluationScopeItem> Evaluations,
    List<ChatReportItem> Reports,
    List<NotificationScopeItem> Notifications);
