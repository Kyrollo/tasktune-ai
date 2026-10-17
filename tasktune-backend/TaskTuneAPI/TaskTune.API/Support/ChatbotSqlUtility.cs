using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaskTuneDbContext = TaskTune.Infrastructure.Models.TaskTuneDbContext;

namespace TaskTune.API.Support;

public static class ChatbotSqlUtility
{
    private static readonly string[] AllowedViews =
    [
        "vw_ChatbotTaskFull",
        "vw_ChatbotEvaluationView",
        "vw_ChatbotNotificationsView"
    ];

    private const string ViewDdl = """
IF OBJECT_ID('dbo.vw_ChatbotTaskFull', 'V') IS NULL EXEC('CREATE VIEW dbo.vw_ChatbotTaskFull AS SELECT 1 AS Placeholder');
GO
ALTER VIEW dbo.vw_ChatbotTaskFull AS
SELECT
    p.CompanyId,
    t.AssignedTo AS ScopeUserId,
    u.DepartmentId AS ScopeDepartmentId,
    t.TaskId,
    t.TaskName,
    t.Description AS TaskDescription,
    t.ProjectId,
    p.ProjectName,
    t.AssignedTo AS AssignedUserId,
    COALESCE(u.FullName, CONCAT(u.FirstName, ' ', u.LastName)) AS AssignedTo,
    u.Email AS AssigneeEmail,
    d.DepartmentName AS Department,
    CASE t.Priority
        WHEN 0 THEN 'Low'
        WHEN 1 THEN 'Medium'
        WHEN 2 THEN 'High'
        WHEN 3 THEN 'Critical'
        ELSE 'Unknown'
    END AS PriorityName,
    CASE t.Status
        WHEN 0 THEN 'Not Started'
        WHEN 1 THEN 'In Progress'
        WHEN 2 THEN 'Completed'
        WHEN 3 THEN 'In Review'
        ELSE 'Unknown'
    END AS StatusName,
    t.DueDate,
    CASE
        WHEN t.Status <> 2 AND t.DueDate < CONVERT(date, GETUTCDATE()) THEN 'Overdue'
        WHEN t.Status <> 2 AND t.DueDate <= DATEADD(day, 7, CONVERT(date, GETUTCDATE())) THEN 'DueSoon'
        ELSE 'OnTrack'
    END AS DeadlineStatus,
    t.CompletionPercentage,
    lp.Notes AS ProgressNotes
FROM dbo.Tasks t
INNER JOIN dbo.Projects p ON p.ProjectId = t.ProjectId
LEFT JOIN dbo.Users u ON u.UserId = t.AssignedTo
LEFT JOIN dbo.Departments d ON d.DepartmentId = u.DepartmentId
LEFT JOIN (
    SELECT tp.TaskId, tp.Notes,
           ROW_NUMBER() OVER (PARTITION BY tp.TaskId ORDER BY tp.SubmittedAt DESC, tp.ProgressId DESC) AS rn
    FROM dbo.TaskProgress tp
) lp ON lp.TaskId = t.TaskId AND lp.rn = 1;
GO
IF OBJECT_ID('dbo.vw_ChatbotEvaluationView', 'V') IS NULL EXEC('CREATE VIEW dbo.vw_ChatbotEvaluationView AS SELECT 1 AS Placeholder');
GO
ALTER VIEW dbo.vw_ChatbotEvaluationView AS
SELECT
    eu.CompanyId,
    e.EvaluatedUserId AS ScopeUserId,
    eu.DepartmentId AS ScopeDepartmentId,
    e.EvaluationId,
    e.TaskId,
    t.TaskName,
    e.EvaluatedUserId,
    COALESCE(eu.FullName, CONCAT(eu.FirstName, ' ', eu.LastName)) AS EvaluatedUser,
    e.EvaluatorId,
    COALESCE(ev.FullName, CONCAT(ev.FirstName, ' ', ev.LastName)) AS EvaluatedBy,
    d.DepartmentName AS Department,
    e.OverallScore,
    e.QualityScore,
    e.TimelinessScore,
    e.CollaborationScore,
    CASE e.RiskLevel
        WHEN 0 THEN 'Low'
        WHEN 1 THEN 'Medium'
        WHEN 2 THEN 'High'
        ELSE 'Unknown'
    END AS RiskLevelName,
    e.ManualFeedback,
    e.CreatedAt
FROM dbo.Evaluations e
INNER JOIN dbo.Users eu ON eu.UserId = e.EvaluatedUserId
LEFT JOIN dbo.Users ev ON ev.UserId = e.EvaluatorId
LEFT JOIN dbo.Tasks t ON t.TaskId = e.TaskId
LEFT JOIN dbo.Departments d ON d.DepartmentId = eu.DepartmentId;
GO
IF OBJECT_ID('dbo.vw_ChatbotNotificationsView', 'V') IS NULL EXEC('CREATE VIEW dbo.vw_ChatbotNotificationsView AS SELECT 1 AS Placeholder');
GO
ALTER VIEW dbo.vw_ChatbotNotificationsView AS
SELECT
    u.CompanyId,
    n.UserId AS ScopeUserId,
    u.DepartmentId AS ScopeDepartmentId,
    n.NotificationId,
    n.UserId,
    COALESCE(u.FullName, CONCAT(u.FirstName, ' ', u.LastName)) AS RecipientName,
    n.Title,
    n.Message,
    CASE n.NotificationType
        WHEN 0 THEN 'Task'
        WHEN 1 THEN 'Deadline'
        WHEN 2 THEN 'Evaluation'
        WHEN 3 THEN 'System'
        ELSE 'Other'
    END AS NotificationTypeName,
    n.RelatedTaskId,
    t.TaskName AS RelatedTask,
    CASE WHEN n.IsRead = 1 THEN 'Read' ELSE 'Unread' END AS ReadStatus,
    n.SentAt
FROM dbo.Notifications n
INNER JOIN dbo.Users u ON u.UserId = n.UserId
LEFT JOIN dbo.Tasks t ON t.TaskId = n.RelatedTaskId;
GO
""";

    private static bool _viewsEnsured;
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);

    public static string ViewSchemaDescription =>
        """
You may query only these SQL Server views:

1. dbo.vw_ChatbotTaskFull
Columns:
CompanyId, ScopeUserId, ScopeDepartmentId, TaskId, TaskName, TaskDescription, ProjectId, ProjectName,
AssignedUserId, AssignedTo, AssigneeEmail, Department, PriorityName, StatusName, DueDate, DeadlineStatus,
CompletionPercentage, ProgressNotes

2. dbo.vw_ChatbotEvaluationView
Columns:
CompanyId, ScopeUserId, ScopeDepartmentId, EvaluationId, TaskId, TaskName, EvaluatedUserId, EvaluatedUser,
EvaluatorId, EvaluatedBy, Department, OverallScore, QualityScore, TimelinessScore, CollaborationScore,
RiskLevelName, ManualFeedback, CreatedAt

3. dbo.vw_ChatbotNotificationsView
Columns:
CompanyId, ScopeUserId, ScopeDepartmentId, NotificationId, UserId, RecipientName, Title, Message,
NotificationTypeName, RelatedTaskId, RelatedTask, ReadStatus, SentAt
""";

    public static async System.Threading.Tasks.Task EnsureViewsAsync(TaskTuneDbContext db)
    {
        if (_viewsEnsured)
        {
            return;
        }

        await EnsureLock.WaitAsync();
        try
        {
            if (_viewsEnsured)
            {
                return;
            }

            var batches = Regex.Split(ViewDdl, @"^\s*GO\s*$", RegexOptions.Multiline)
                .Where(batch => !string.IsNullOrWhiteSpace(batch));

            foreach (var batch in batches)
            {
                await db.Database.ExecuteSqlRawAsync(batch);
            }

            _viewsEnsured = true;
        }
        finally
        {
            EnsureLock.Release();
        }
    }

    public static bool IsQueryAllowed(string sql, string role, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(sql))
        {
            error = "The generated SQL was empty.";
            return false;
        }

        sql = StripMarkdown(sql).Trim();

        if (sql.Contains(';'))
        {
            error = "Multiple SQL statements are not allowed.";
            return false;
        }

        if (!Regex.IsMatch(sql, @"^\s*(select|with)\b", RegexOptions.IgnoreCase))
        {
            error = "Only SELECT queries are allowed.";
            return false;
        }

        if (Regex.IsMatch(sql, @"\b(insert|update|delete|drop|alter|create|merge|truncate|exec|execute)\b", RegexOptions.IgnoreCase))
        {
            error = "Write operations are not allowed.";
            return false;
        }

        var matches = Regex.Matches(sql, @"\b(?:from|join)\s+(?:\[?dbo\]?\.)?\[?(vw_Chatbot\w+)\]?", RegexOptions.IgnoreCase);
        if (matches.Count == 0)
        {
            error = "Queries must read from the approved chatbot views only.";
            return false;
        }

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            if (!AllowedViews.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                error = $"The view '{name}' is not allowed.";
                return false;
            }

            if (string.Equals(role, "HR", StringComparison.OrdinalIgnoreCase)
                && string.Equals(name, "vw_ChatbotTaskFull", StringComparison.OrdinalIgnoreCase))
            {
                error = "HR access cannot query task-detail rows.";
                return false;
            }
        }

        return true;
    }

    public static async System.Threading.Tasks.Task<(List<Dictionary<string, object?>> Rows, string? Error, string ExecutedSql)> ExecuteScopedQueryAsync(
        TaskTuneDbContext db,
        string sql,
        int companyId,
        string role,
        int userId,
        int? departmentId,
        int maxRows = 25)
    {
        sql = StripMarkdown(sql).Trim();
        sql = Regex.Replace(sql, @"\border\s+by\b[\s\S]*$", string.Empty, RegexOptions.IgnoreCase).Trim();

        if (!IsQueryAllowed(sql, role, out var validationError))
        {
            return ([], validationError, sql);
        }

        var scopeFilter = role switch
        {
            "Employee" => "AND ScopeUserId = @userId",
            "Supervisor" when departmentId.HasValue => "AND (ScopeDepartmentId = @departmentId OR ScopeUserId = @userId)",
            "Supervisor" => "AND ScopeUserId = @userId",
            "HR" => string.Empty,
            _ => string.Empty
        };

        var wrappedSql = $"""
WITH agent_query AS (
{sql}
)
SELECT TOP (@maxRows) *
FROM agent_query
WHERE CompanyId = @companyId
{scopeFilter}
""";

        var connection = db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = wrappedSql;
        AddParameter(command, "@maxRows", maxRows);
        AddParameter(command, "@companyId", companyId);
        AddParameter(command, "@userId", userId);
        AddParameter(command, "@departmentId", departmentId ?? (object)DBNull.Value);

        try
        {
            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            return (rows, null, wrappedSql);
        }
        catch (Exception ex)
        {
            return ([], ex.Message, wrappedSql);
        }
        finally
        {
            if (shouldCloseConnection && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static string GetRoleSqlRules(string role, int userId, int companyId, int? departmentId, string userName)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Current role: {role}");
        builder.AppendLine($"Current user id: {userId}");
        builder.AppendLine($"Current user name: {userName}");
        builder.AppendLine($"Current company id: {companyId}");

        if (departmentId.HasValue)
        {
            builder.AppendLine($"Current department id: {departmentId.Value}");
        }

        builder.AppendLine("Every query must project CompanyId, ScopeUserId, and ScopeDepartmentId from the chosen view.");

        if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("Employee rule: only ask about the current employee's own rows.");
        }
        else if (string.Equals(role, "Supervisor", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("Supervisor rule: stay within the user's own department when possible.");
        }
        else if (string.Equals(role, "HR", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("HR rule: do not query dbo.vw_ChatbotTaskFull.");
        }
        else
        {
            builder.AppendLine("Manager, CEO, and Admin may use company-wide rows.");
        }

        builder.AppendLine("Use TOP 25 for list-style queries and avoid ORDER BY unless absolutely necessary.");
        builder.AppendLine("Return SQL only. No markdown and no explanation.");
        return builder.ToString();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string StripMarkdown(string text)
    {
        return text.Replace("```sql", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
