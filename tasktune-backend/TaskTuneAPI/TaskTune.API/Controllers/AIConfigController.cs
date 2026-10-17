using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AIConfigController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public AIConfigController(TaskTuneDbContext db) => _db = db;

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        if (!await HasPageAccessAsync(_db, "ai", "CEO", "Admin"))
        {
            return Forbid();
        }

        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var config = await AIConfigurationStore.LoadAsync(_db, companyId.Value);
        return Ok(AIConfigurationStore.ToClientResponse(config));
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] TaskTuneAIConfiguration incoming)
    {
        if (!await HasPageAccessAsync(_db, "ai", "CEO", "Admin"))
        {
            return Forbid();
        }

        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });
        if (userId == null) return Unauthorized();
        if (incoming == null) return BadRequest(new { message = "Configuration payload is required." });

        var before = await AIConfigurationStore.LoadAsync(_db, companyId.Value);
        var saved = await AIConfigurationStore.SaveAsync(_db, companyId.Value, incoming);

        _db.AIActionsLogs.Add(new AIActionsLog
        {
            ActionType = 6,
            UserId = userId.Value,
            InputData = System.Text.Json.JsonSerializer.Serialize(before),
            OutputData = System.Text.Json.JsonSerializer.Serialize(saved),
            ModelVersion = "ai_control_panel",
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            userId,
            action: "UPDATE",
            resourceType: "AI_Config",
            resourceId: companyId.Value,
            before: before,
            after: saved);

        return Ok(new
        {
            message = "AI configuration updated successfully.",
            config = AIConfigurationStore.ToClientResponse(saved)
        });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetAILogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!await HasPageAccessAsync(_db, "ai", "CEO", "Admin"))
        {
            return Forbid();
        }

        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var query = _db.AIActionsLogs
            .Include(l => l.User)
            .Include(l => l.Task)
                .ThenInclude(t => t.Project)
            .Where(l =>
                (l.User != null && l.User.CompanyId == companyId.Value) ||
                (l.Task != null && l.Task.Project != null && l.Task.Project.CompanyId == companyId.Value));

        var total = await query.CountAsync();
        var rawItems = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.AiLogId,
                l.TaskId,
                TaskName = l.Task != null ? l.Task.TaskName : null,
                l.UserId,
                UserName = l.User != null ? (l.User.FullName ?? (l.User.FirstName + " " + l.User.LastName)) : null,
                l.ActionType,
                l.InputData,
                l.OutputData,
                l.ModelVersion,
                l.Timestamp
            })
            .ToListAsync();

        var items = rawItems.Select(l => new
        {
            l.AiLogId,
            l.TaskId,
            l.TaskName,
            l.UserId,
            l.UserName,
            l.ActionType,
            ActionTypeName = GetActionTypeName(l.ActionType),
            l.InputData,
            InputPreview = l.InputData != null && l.InputData.Length > 160 ? l.InputData.Substring(0, 160) + "..." : l.InputData,
            l.OutputData,
            OutputPreview = l.OutputData != null && l.OutputData.Length > 160 ? l.OutputData.Substring(0, 160) + "..." : l.OutputData,
            l.ModelVersion,
            l.Timestamp
        });

        var usage = await query
            .GroupBy(l => 1)
            .Select(g => new
            {
                TotalActions = g.Count(),
                MonthActions = g.Count(x => x.Timestamp >= DateTime.UtcNow.AddDays(-30))
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            items,
            totalCount = total,
            usage = new
            {
                totalActions = usage?.TotalActions ?? 0,
                monthActions = usage?.MonthActions ?? 0
            }
        });
    }

    private static string GetActionTypeName(byte actionType) => actionType switch
    {
        1 => "Task Guidance",
        2 => "Progress Analysis",
        3 => "Report Generation",
        4 => "Chat Assistant",
        5 => "Email Assistant",
        6 => "AI Configuration",
        _ => "AI Action"
    };
}
