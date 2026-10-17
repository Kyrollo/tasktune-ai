using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public DashboardsController(TaskTuneDbContext db) => _db = db;

    [HttpGet("employee")]
    public async Task<IActionResult> EmployeeDashboard()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var tasks = await _db.Tasks.Where(t => t.AssignedTo == userId.Value).ToListAsync();

        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);

        var completedByMonth = tasks
            .Where(t => t.Status == 2 && t.UpdatedAt >= sixMonthsAgo)
            .GroupBy(t => new { t.UpdatedAt.Year, t.UpdatedAt.Month })
            .ToDictionary(g => g.Key, g => g.Count());

        var completionTrend = Enumerable.Range(0, 6).Select(i =>
        {
            var date = sixMonthsAgo.AddMonths(i);
            var key = new { date.Year, date.Month };
            return new
            {
                month = date.ToString("MMM yyyy"),
                completed = completedByMonth.GetValueOrDefault(key, 0)
            };
        }).ToList();

        var statusMap = new Dictionary<int, (string Name, string Color)>
        {
            { 0, ("Not Started", "#6B7280") },
            { 1, ("In Progress", "#F97316") },
            { 2, ("Completed", "#22C55E") },
            { 3, ("In Review", "#3B82F6") }
        };

        var taskStatusDistribution = tasks
            .GroupBy(t => t.Status)
            .Where(g => statusMap.ContainsKey(g.Key))
            .Select(g => new
            {
                name = statusMap[g.Key].Name,
                value = g.Count(),
                color = statusMap[g.Key].Color
            })
            .ToList();

        return Ok(new
        {
            TotalTasks = tasks.Count,
            CompletedTasks = tasks.Count(t => t.Status == 2),
            ActiveTasks = tasks.Count(t => t.Status == 1),
            OverdueTasks = tasks.Count(t => t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) && t.Status != 2),
            AverageScore = Math.Round(
                await _db.Evaluations.Where(e => e.EvaluatedUserId == userId.Value)
                    .AverageAsync(e => (double?)e.OverallScore) ?? 0, 2),
            CompletionTrend = completionTrend,
            TaskStatusDistribution = taskStatusDistribution
        });
    }

    [HttpGet("supervisor")]
    [Authorize(Roles = "Supervisor,Manager,CEO,Admin")]
    public async Task<IActionResult> SupervisorDashboard()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        return Ok(new
        {
            TotalTasks = await _db.Tasks.CountAsync(t => t.Project.CompanyId == companyId.Value),
            PendingReviews = await _db.Evaluations.CountAsync(e => e.Status == 0 && e.Task.Project.CompanyId == companyId.Value)
        });
    }

    [HttpGet("executive")]
    [Authorize(Roles = "Manager,CEO,Admin")]
    public async Task<IActionResult> ExecutiveDashboard()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var projects = await _db.Projects.Where(p => p.CompanyId == companyId.Value).ToListAsync();
        var tasks = await _db.Tasks.Include(t => t.Project).Where(t => t.Project.CompanyId == companyId.Value).ToListAsync();

        // Department Performance: use raw query to safely get averages
        var deptPerformance = await _db.Database
            .SqlQueryRaw<DeptScoreDto>(
                @"SELECT d.DepartmentName AS Name, 
                  ISNULL(ROUND(AVG(CAST(e.OverallScore AS FLOAT)), 1), 0) AS Score
                  FROM Departments d
                  LEFT JOIN Users u ON u.DepartmentId = d.DepartmentId
                  LEFT JOIN Evaluations e ON e.EvaluatedUserId = u.UserId
                  WHERE d.CompanyId = {0}
                  GROUP BY d.DepartmentName", companyId.Value)
            .ToListAsync();

        // Performance Trend: monthly averages for last 6 months
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);

        var monthlyEvals = await _db.Database
            .SqlQueryRaw<MonthlyEvalDto>(
                @"SELECT 
                    YEAR(e.CreatedAt) AS Yr, MONTH(e.CreatedAt) AS Mo,
                    ROUND(AVG(CAST(e.OverallScore AS FLOAT)), 1) AS AvgOverall,
                    ROUND(AVG(CAST(e.QualityScore AS FLOAT)), 1) AS AvgQuality,
                    ROUND(AVG(CAST(e.TimelinessScore AS FLOAT)), 1) AS AvgTimeliness
                  FROM Evaluations e
                  INNER JOIN Tasks t ON e.TaskId = t.TaskId
                  INNER JOIN Projects p ON t.ProjectId = p.ProjectId
                  WHERE p.CompanyId = {0} AND e.CreatedAt >= {1}
                  GROUP BY YEAR(e.CreatedAt), MONTH(e.CreatedAt)", companyId.Value, sixMonthsAgo)
            .ToListAsync();

        var performanceTrend = Enumerable.Range(0, 6).Select(i =>
        {
            var date = sixMonthsAgo.AddMonths(i);
            var m = monthlyEvals.FirstOrDefault(x => x.Yr == date.Year && x.Mo == date.Month);
            return new
            {
                month = date.ToString("MMM yyyy"),
                overall = m?.AvgOverall ?? 0,
                quality = m?.AvgQuality ?? 0,
                timeliness = m?.AvgTimeliness ?? 0
            };
        }).ToList();

        // Top Performers: top 5 users by average evaluation score
        var topPerformersResult = await _db.Database
            .SqlQueryRaw<TopPerformerDto>(
                @"SELECT TOP 5 
                    u.UserId, u.FullName, 
                    ISNULL(d.DepartmentName, 'N/A') AS Department,
                    ROUND(AVG(CAST(e.OverallScore AS FLOAT)), 1) AS OverallScore
                  FROM Evaluations e
                  INNER JOIN Users u ON e.EvaluatedUserId = u.UserId
                  INNER JOIN Tasks t ON e.TaskId = t.TaskId
                  INNER JOIN Projects p ON t.ProjectId = p.ProjectId
                  LEFT JOIN Departments d ON u.DepartmentId = d.DepartmentId
                  WHERE p.CompanyId = {0}
                  GROUP BY u.UserId, u.FullName, d.DepartmentName
                  ORDER BY AVG(CAST(e.OverallScore AS FLOAT)) DESC", companyId.Value)
            .ToListAsync();


        return Ok(new
        {
            TotalProjects = projects.Count,
            ActiveProjects = projects.Count(p => p.Status == 1),
            CompletedProjects = projects.Count(p => p.Status == 2),
            TotalUsers = await _db.Users.CountAsync(u => u.CompanyId == companyId.Value && u.IsActive),
            TotalTasks = tasks.Count,
            CompletedTasks = tasks.Count(t => t.Status == 2),
            OverdueTasks = tasks.Count(t => t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) && t.Status != 2),
            Departments = deptPerformance,
            PerformanceTrend = performanceTrend,
            TopPerformers = topPerformersResult,
            RiskAlerts = tasks
                .Where(t => t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) && t.Status != 2)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .Select(t => new
                {
                    type = "overdue",
                    severity = "high",
                    message = $"{t.TaskName} is overdue",
                    detail = $"{t.Project?.ProjectName ?? "Project"} deadline was {t.DueDate:yyyy-MM-dd}"
                })
                .ToList()
        });
    }

    [HttpGet("hr")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> HRDashboard()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var users = await _db.Users.Where(u => u.CompanyId == companyId.Value).ToListAsync();

        // Use raw SQL to get eval stats safely
        var avgScore = await _db.Database
            .SqlQueryRaw<double?>(
                @"SELECT AVG(CAST(e.OverallScore AS FLOAT)) AS Value
                  FROM Evaluations e
                  INNER JOIN Tasks t ON e.TaskId = t.TaskId
                  INNER JOIN Projects p ON t.ProjectId = p.ProjectId
                  WHERE p.CompanyId = {0}", companyId.Value)
            .FirstOrDefaultAsync() ?? 0;

        var lowPerformers = await _db.Database
            .SqlQueryRaw<int>(
                @"SELECT COUNT(*) AS Value FROM (
                    SELECT e.EvaluatedUserId
                    FROM Evaluations e
                    INNER JOIN Tasks t ON e.TaskId = t.TaskId
                    INNER JOIN Projects p ON t.ProjectId = p.ProjectId
                    WHERE p.CompanyId = {0}
                    GROUP BY e.EvaluatedUserId
                    HAVING AVG(CAST(e.OverallScore AS FLOAT)) < 50
                  ) x", companyId.Value)
            .FirstOrDefaultAsync();

        var departmentEngagement = await _db.Departments
            .Where(d => d.CompanyId == companyId.Value)
            .Select(d => new
            {
                dept = d.DepartmentName,
                avg = Math.Round(
                    d.Users
                        .SelectMany(u => u.EvaluationEvaluatedUsers)
                        .Average(e => (double?)e.OverallScore) ?? 0, 1)
            })
            .ToListAsync();

        var weeklyRanges = Enumerable.Range(0, 4)
            .Select(offset =>
            {
                var end = DateTime.UtcNow.Date.AddDays(-(offset * 7));
                var start = end.AddDays(-6);
                return new { start, end };
            })
            .Reverse()
            .ToList();

        var engagementHeatmap = await _db.Departments
            .Where(d => d.CompanyId == companyId.Value)
            .Select(d => new
            {
                department = d.DepartmentName,
                week1 = Math.Round(d.Users
                    .SelectMany(u => u.EvaluationEvaluatedUsers)
                    .Where(e => e.CreatedAt >= weeklyRanges[0].start && e.CreatedAt <= weeklyRanges[0].end)
                    .Average(e => (double?)e.OverallScore) ?? 0, 1),
                week2 = Math.Round(d.Users
                    .SelectMany(u => u.EvaluationEvaluatedUsers)
                    .Where(e => e.CreatedAt >= weeklyRanges[1].start && e.CreatedAt <= weeklyRanges[1].end)
                    .Average(e => (double?)e.OverallScore) ?? 0, 1),
                week3 = Math.Round(d.Users
                    .SelectMany(u => u.EvaluationEvaluatedUsers)
                    .Where(e => e.CreatedAt >= weeklyRanges[2].start && e.CreatedAt <= weeklyRanges[2].end)
                    .Average(e => (double?)e.OverallScore) ?? 0, 1),
                week4 = Math.Round(d.Users
                    .SelectMany(u => u.EvaluationEvaluatedUsers)
                    .Where(e => e.CreatedAt >= weeklyRanges[3].start && e.CreatedAt <= weeklyRanges[3].end)
                    .Average(e => (double?)e.OverallScore) ?? 0, 1)
            })
            .ToListAsync();

        var lowPerformersList = await _db.Users
            .Include(u => u.Department)
            .Where(u => u.CompanyId == companyId.Value && u.IsActive)
            .Select(u => new
            {
                employeeName = u.FullName ?? (u.FirstName + " " + u.LastName),
                department = u.Department != null ? u.Department.DepartmentName : "Unassigned",
                averageScore = Math.Round(u.EvaluationEvaluatedUsers.Average(e => (double?)e.OverallScore) ?? 0, 1),
                delayCount = u.TaskAssignedToNavigations.Count(t => t.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) && t.Status != 2)
            })
            .Where(u => (u.averageScore > 0 && u.averageScore < 50) || u.delayCount >= 2)
            .OrderBy(u => u.averageScore)
            .ThenByDescending(u => u.delayCount)
            .Take(8)
            .ToListAsync();

        return Ok(new
        {
            TotalEmployees = users.Count(u => u.IsActive),
            InactiveEmployees = users.Count(u => !u.IsActive),
            AverageScore = Math.Round(avgScore, 2),
            LowPerformers = lowPerformers,
            RecentCommunications = await _db.CommunicationLogs
                .CountAsync(c => c.CompanyId == companyId.Value && c.SentAt > DateTime.UtcNow.AddDays(-30)),
            DepartmentCount = await _db.Departments.CountAsync(d => d.CompanyId == companyId.Value),
            EngagementData = departmentEngagement,
            EngagementHeatmap = engagementHeatmap,
            LowPerformersList = lowPerformersList.Select(u => new
            {
                u.employeeName,
                u.department,
                u.averageScore,
                u.delayCount,
                reason = u.averageScore > 0 && u.averageScore < 50
                    ? $"Average score is {u.averageScore}%"
                    : $"{u.delayCount} overdue tasks"
            })
        });
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDashboard()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        return Ok(new
        {
            TotalUsers = await _db.Users.CountAsync(u => u.CompanyId == companyId.Value),
            ActiveUsers = await _db.Users.CountAsync(u => u.CompanyId == companyId.Value && u.IsActive),
            TotalProjects = await _db.Projects.CountAsync(p => p.CompanyId == companyId.Value),
            TotalTasks = await _db.Tasks.CountAsync(t => t.Project.CompanyId == companyId.Value),
            RecentAuditLogs = await _db.AuditLogs.CountAsync(l => l.Timestamp > DateTime.UtcNow.AddDays(-7)),
            AIActionsToday = await _db.AIActionsLogs.CountAsync(a => a.Timestamp > DateTime.UtcNow.AddDays(-1))
        });
    }
}

public record DeptScoreDto(string Name, double Score);
public record MonthlyEvalDto(int Yr, int Mo, double AvgOverall, double AvgQuality, double AvgTimeliness);
public record TopPerformerDto(int UserId, string FullName, string Department, double OverallScore);
