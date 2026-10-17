using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public AuditLogsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? resourceType,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!await HasPageAccessAsync(_db, "audit", "Manager", "CEO", "Admin"))
        {
            return Forbid();
        }

        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var query = _db.AuditLogs
            .Include(l => l.User)
            .Where(l => l.User.CompanyId == companyId.Value)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(l => l.ResourceType == resourceType);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.LogId,
                l.UserId,
                UserName = l.User != null ? (l.User.FullName ?? (l.User.FirstName + " " + l.User.LastName)) : null,
                l.Action,
                l.ResourceType,
                l.ResourceId,
                l.ChangesJson,
                l.IpAddress,
                l.UserAgent,
                l.Timestamp
            })
            .ToListAsync();

        return Ok(new { items, totalCount = total });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? resourceType,
        [FromQuery] string? action)
    {
        if (!await HasPageAccessAsync(_db, "audit", "Manager", "CEO", "Admin"))
        {
            return Forbid();
        }

        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var query = _db.AuditLogs
            .Include(l => l.User)
            .Where(l => l.User.CompanyId == companyId.Value)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(l => l.ResourceType == resourceType);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new
            {
                l.LogId,
                UserName = l.User != null ? (l.User.FullName ?? (l.User.FirstName + " " + l.User.LastName)) : null,
                l.Action,
                l.ResourceType,
                l.ResourceId,
                l.IpAddress,
                l.Timestamp,
                l.ChangesJson
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("LogId,User,Action,ResourceType,ResourceId,IpAddress,Timestamp,ChangesJson");
        foreach (var item in items)
        {
            sb.AppendLine(
                $"{item.LogId},\"{Escape(item.UserName)}\",\"{Escape(item.Action)}\",\"{Escape(item.ResourceType)}\",{item.ResourceId},\"{Escape(item.IpAddress)}\",\"{item.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{Escape(item.ChangesJson)}\"");
        }

        if (userId != null)
        {
            await AuditLogWriter.WriteAsync(
                _db,
                HttpContext,
                userId,
                action: "EXPORT",
                resourceType: "Audit_Log",
                resourceId: 0,
                after: new { resourceType, action, count = items.Count });
        }

        return File(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\"", "\"\"");
}
