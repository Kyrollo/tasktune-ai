using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public ReportsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var query = _db.Reports
            .Include(r => r.GeneratedByNavigation)
            .Where(r => r.CompanyId == companyId.Value);

        var total = await query.CountAsync();
        var rawItems = await query
            .OrderByDescending(r => r.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.ReportId,
                r.CompanyId,
                r.GeneratedBy,
                GeneratedByName = r.GeneratedByNavigation != null ? (r.GeneratedByNavigation.FullName ?? r.GeneratedByNavigation.FirstName + " " + r.GeneratedByNavigation.LastName) : null,
                r.ReportType,
                r.ReportPeriodStart,
                r.ReportPeriodEnd,
                r.FilePath,
                r.MetadataJson,
                r.GeneratedAt
            })
            .ToListAsync();

        var items = rawItems.Select(r => new
        {
            r.ReportId,
            r.CompanyId,
            r.GeneratedBy,
            r.GeneratedByName,
            r.ReportType,
            ReportTypeName = GetReportTypeName(r.ReportType),
            r.ReportPeriodStart,
            r.ReportPeriodEnd,
            r.FilePath,
            r.MetadataJson,
            r.GeneratedAt,
            Title = TryGetReportTitle(r.MetadataJson) ?? $"{GetReportTypeName(r.ReportType)} Report",
            Summary = TryGetReportSummary(r.MetadataJson)
        });

        return Ok(new { items, totalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReport(int id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var report = await _db.Reports
            .Include(r => r.GeneratedByNavigation)
            .FirstOrDefaultAsync(r => r.ReportId == id && r.CompanyId == companyId.Value);

        if (report == null) return NotFound();

        return Ok(BuildReportResponse(report));
    }

    [HttpPost("generate")]
    [Authorize(Roles = "Supervisor,Manager,CEO,HR,Admin")]
    public async Task<IActionResult> GenerateReport([FromBody] GenerateReportRequest request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null) return Unauthorized();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var start = request.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var end = request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        List<ChartPoint> chartData;
        byte reportTypeByte;

        switch (request.ReportType?.ToLowerInvariant())
        {
            case "performance":
                reportTypeByte = 1;
                chartData = await GetPerformanceData(companyId.Value, start, end);
                break;
            case "department":
                reportTypeByte = 2;
                chartData = await GetDepartmentData(companyId.Value, start, end);
                break;
            case "project":
                reportTypeByte = 3;
                chartData = await GetProjectData(companyId.Value, start, end);
                break;
            case "user":
                reportTypeByte = 4;
                chartData = await GetUserData(companyId.Value, start, end);
                break;
            default:
                return BadRequest(new { message = "Invalid reportType. Must be: performance, department, project, or user." });
        }

        var metadataJson = JsonSerializer.Serialize(new { chartData }, CamelCase);

        var report = new Report
        {
            CompanyId = companyId.Value,
            GeneratedBy = userId.Value,
            ReportType = reportTypeByte,
            ReportPeriodStart = start,
            ReportPeriodEnd = end,
            MetadataJson = metadataJson,
            GeneratedAt = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            report.ReportId,
            report.ReportType,
            reportTypeName = request.ReportType,
            report.ReportPeriodStart,
            report.ReportPeriodEnd,
            report.GeneratedAt,
            chartData
        });
    }

    [HttpPost]
    [Authorize(Roles = "Supervisor,Manager,CEO,HR,Admin")]
    public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null) return Unauthorized();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var report = new Report
        {
            CompanyId = companyId.Value,
            GeneratedBy = userId.Value,
            ReportType = request.ReportType,
            ReportPeriodStart = request.ReportPeriodStart,
            ReportPeriodEnd = request.ReportPeriodEnd,
            FilePath = request.FilePath,
            MetadataJson = request.MetadataJson,
            GeneratedAt = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            report.ReportId,
            report.ReportType,
            report.GeneratedAt
        });
    }
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task<List<ChartPoint>> GetPerformanceData(int companyId, DateOnly start, DateOnly end)
    {
        var startDt = start.ToDateTime(TimeOnly.MinValue);
        var endDt = end.ToDateTime(TimeOnly.MaxValue);

        var rows = await _db.Evaluations
            .Where(e => e.EvaluatedUser.CompanyId == companyId
                        && e.CreatedAt >= startDt && e.CreatedAt <= endDt)
            .Select(e => new
            {
                Dept = e.EvaluatedUser.Department != null
                    ? e.EvaluatedUser.Department.DepartmentName : "Unassigned",
                Avg = (e.OverallScore + e.QualityScore + e.TimelinessScore) / 3m
            })
            .ToListAsync();

        return rows.GroupBy(r => r.Dept)
            .Select(g => new ChartPoint(g.Key, Math.Round((double)g.Average(x => x.Avg), 1)))
            .ToList();
    }

    private async Task<List<ChartPoint>> GetDepartmentData(int companyId, DateOnly start, DateOnly end)
    {
        var rows = await _db.Tasks
            .Where(t => t.Project.CompanyId == companyId
                        && t.DueDate >= start && t.StartDate <= end)
            .Select(t => new
            {
                Dept = t.AssignedToNavigation.Department != null
                    ? t.AssignedToNavigation.Department.DepartmentName : "Unassigned",
                t.Status
            })
            .ToListAsync();

        return rows.GroupBy(r => r.Dept)
            .Select(g => new ChartPoint(g.Key, Math.Round(100.0 * g.Count(t => t.Status == 3) / g.Count(), 1)))
            .ToList();
    }

    private async Task<List<ChartPoint>> GetProjectData(int companyId, DateOnly start, DateOnly end)
    {
        var rows = await _db.Projects
            .Where(p => p.CompanyId == companyId && p.StartDate <= end && p.EndDate >= start)
            .Select(p => new
            {
                Name = p.ProjectName,
                Pct = p.Tasks.Count == 0 ? 0.0 : p.Tasks.Average(t => (double)t.CompletionPercentage)
            })
            .ToListAsync();

        return rows.Select(r => new ChartPoint(r.Name, Math.Round(r.Pct, 1))).ToList();
    }

    private async Task<List<ChartPoint>> GetUserData(int companyId, DateOnly start, DateOnly end)
    {
        var startDt = start.ToDateTime(TimeOnly.MinValue);
        var endDt = end.ToDateTime(TimeOnly.MaxValue);

        var rows = await _db.Evaluations
            .Where(e => e.EvaluatedUser.CompanyId == companyId
                        && e.CreatedAt >= startDt && e.CreatedAt <= endDt)
            .Select(e => new
            {
                Name = e.EvaluatedUser.FullName ?? (e.EvaluatedUser.FirstName + " " + e.EvaluatedUser.LastName),
                e.OverallScore
            })
            .ToListAsync();

        return rows.GroupBy(r => r.Name)
            .Select(g => new ChartPoint(g.Key, Math.Round((double)g.Average(x => x.OverallScore), 1)))
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToList();
    }

    [HttpPost("ai-generate")]
    [Authorize(Roles = "Supervisor,Manager,CEO,HR,Admin")]
    public async Task<IActionResult> GenerateAIReport([FromBody] AIGenerateReportRequest request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null) return Unauthorized();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var user = await _db.Users.FindAsync(userId.Value);
        var aiConfig = await AIConfigurationStore.LoadAsync(_db, companyId.Value);
        var agentConfig = aiConfig.GetAgent("report_generation");
        if (!aiConfig.Enabled || !aiConfig.Features.ReportGeneration || !agentConfig.Enabled)
        {
            return StatusCode(503, new { message = "Report generation is disabled in AI Control Panel." });
        }
        var startDate = ParseDateOnly(request.StartDate) ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var endDate = ParseDateOnly(request.EndDate) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var selectedDepartment = string.IsNullOrWhiteSpace(request.Department) ? "All Departments" : request.Department.Trim();

        // Gather task data for the agent
        var tasksQuery = _db.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedToNavigation)
                .ThenInclude(u => u.Department)
            .Where(t => t.Project.CompanyId == companyId.Value)
            .Where(t => t.StartDate <= endDate && t.DueDate >= startDate);

        if (!selectedDepartment.Equals("All Departments", StringComparison.OrdinalIgnoreCase))
        {
            tasksQuery = tasksQuery.Where(t =>
                t.AssignedToNavigation.Department != null &&
                t.AssignedToNavigation.Department.DepartmentName == selectedDepartment);
        }

        var tasks = await tasksQuery
            .Select(t => new
            {
                task_id = t.TaskId.ToString(),
                task_name = t.TaskName,
                project_id = t.ProjectId.ToString(),
                project_name = t.Project.ProjectName,
                employee_id = t.AssignedTo.ToString(),
                employee_name = t.AssignedToNavigation.FullName ?? (t.AssignedToNavigation.FirstName + " " + t.AssignedToNavigation.LastName),
                completion_percentage = t.CompletionPercentage,
                due_date = t.DueDate.ToString("yyyy-MM-dd"),
                status = t.CompletionPercentage >= 100 || t.Status == 2 || t.Status == 3 ? "completed" : t.Status == 1 ? "in-progress" : "not-started",
                department = t.AssignedToNavigation.Department != null ? t.AssignedToNavigation.Department.DepartmentName : "Unassigned"
            })
            .ToListAsync();

        var agentPayload = new
        {
            report_type = request.ReportType ?? "performance",
            start_date = startDate.ToString("yyyy-MM-dd"),
            end_date = endDate.ToString("yyyy-MM-dd"),
            department = selectedDepartment,
            company_id = companyId.Value.ToString(),
            manager_id = userId.Value.ToString(),
            manager_name = user?.FullName ?? "",
            manager_email = user?.Email ?? "",
            run_date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            tasks = tasks,
            total_tasks = tasks.Count,
            total_employees = tasks.Select(t => t.employee_id).Distinct().Count(),
            agent_config = new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                api_key = agentConfig.ApiKey,
                temperature = agentConfig.Temperature,
                max_tokens = agentConfig.MaxTokens
            }
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
        try
        {
            var agentBaseUrl = string.IsNullOrWhiteSpace(agentConfig.BaseUrl)
                ? (Environment.GetEnvironmentVariable("AGENT_REPORT_URL") ?? "http://localhost:8004")
                : agentConfig.BaseUrl;
            var endpoint = string.IsNullOrWhiteSpace(agentConfig.Endpoint) ? "/api/report/generate" : agentConfig.Endpoint;
            var response = await http.PostAsJsonAsync($"{agentBaseUrl.TrimEnd('/')}{endpoint}", agentPayload);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return StatusCode(502, new { message = "Report agent returned an error.", detail = err });
            }
            var rawJson = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(rawJson, CamelCase) ?? new();

            var reportType = MapReportType(request.ReportType);
            _db.AIActionsLogs.Add(new AIActionsLog
            {
                ActionType = 3,
                UserId = userId.Value,
                InputData = JsonSerializer.Serialize(agentPayload, CamelCase),
                OutputData = rawJson,
                ModelVersion = agentConfig.Model,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            await AuditLogWriter.WriteAsync(
                _db,
                HttpContext,
                userId,
                action: "GENERATE",
                resourceType: "AI_Report",
                resourceId: reportType,
                after: new
                {
                    reportType = request.ReportType,
                    startDate,
                    endDate,
                    department = selectedDepartment,
                    generatedAt = DateTime.UtcNow,
                    savedToDatabase = false
                });

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["reportId"] = null,
                ["reportType"] = reportType,
                ["reportTypeName"] = GetReportTypeName(reportType),
                ["reportPeriodStart"] = startDate,
                ["reportPeriodEnd"] = endDate,
                ["generatedAt"] = DateTime.UtcNow,
                ["generatedBy"] = userId.Value,
                ["generatedByName"] = user?.FullName ?? (user?.FirstName + " " + user?.LastName),
                ["savedToDatabase"] = false,
                ["title"] = parsed.GetValueOrDefault("title") ?? $"{GetReportTypeName(reportType)} Report",
                ["summary"] = parsed.GetValueOrDefault("summary")
            };

            foreach (var kv in parsed)
            {
                result[kv.Key] = kv.Value;
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Failed to reach AI report agent.", detail = ex.Message });
        }
    }

    private Dictionary<string, object?> BuildReportResponse(Report report)
    {
        var response = new Dictionary<string, object?>
        {
            ["reportId"] = report.ReportId,
            ["reportType"] = report.ReportType,
            ["reportTypeName"] = GetReportTypeName(report.ReportType),
            ["reportPeriodStart"] = report.ReportPeriodStart,
            ["reportPeriodEnd"] = report.ReportPeriodEnd,
            ["generatedAt"] = report.GeneratedAt,
            ["generatedBy"] = report.GeneratedBy,
            ["generatedByName"] = report.GeneratedByNavigation != null
                ? (report.GeneratedByNavigation.FullName ?? (report.GeneratedByNavigation.FirstName + " " + report.GeneratedByNavigation.LastName))
                : null,
            ["title"] = TryGetReportTitle(report.MetadataJson) ?? $"{GetReportTypeName(report.ReportType)} Report",
            ["summary"] = TryGetReportSummary(report.MetadataJson),
            ["metadata"] = ParseMetadata(report.MetadataJson)
        };

        return response;
    }

    private static object? ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<object>(metadataJson, CamelCase);
        }
        catch
        {
            return metadataJson;
        }
    }

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

    private static string? TryGetReportSummary(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty("ai_analysis", out var analysis)
                && analysis.ValueKind == JsonValueKind.Object
                && analysis.TryGetProperty("summary", out var summary))
            {
                return summary.GetString();
            }
        }
        catch { }

        return null;
    }

    private static string GetReportTypeName(byte reportType) => reportType switch
    {
        1 => "Performance",
        2 => "Department",
        3 => "Project",
        4 => "User",
        _ => "General"
    };

    private static byte MapReportType(string? reportType) => reportType?.ToLowerInvariant() switch
    {
        "performance" => 1,
        "department" => 2,
        "project" => 3,
        "user" => 4,
        _ => 1
    };

    private static DateOnly? ParseDateOnly(string? value)
    {
        return DateOnly.TryParse(value, out var parsed) ? parsed : null;
    }
}

public record ChartPoint(string Dept, double Score);

public record GenerateReportRequest(string ReportType, DateOnly? StartDate, DateOnly? EndDate);

public record CreateReportRequest(
    byte ReportType,
    DateOnly ReportPeriodStart,
    DateOnly ReportPeriodEnd,
    string? FilePath,
    string? MetadataJson);

public record AIGenerateReportRequest(
    string? ReportType,
    string? StartDate,
    string? EndDate,
    string? Department);
