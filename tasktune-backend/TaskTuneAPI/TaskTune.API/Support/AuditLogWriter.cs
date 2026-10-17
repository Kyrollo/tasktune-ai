using System.Text.Json;
using AuditLog = TaskTune.Infrastructure.Models.AuditLog;
using TaskTuneDbContext = TaskTune.Infrastructure.Models.TaskTuneDbContext;

namespace TaskTune.API.Support;

public static class AuditLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async System.Threading.Tasks.Task WriteAsync(
        TaskTuneDbContext db,
        HttpContext httpContext,
        int? userId,
        string action,
        string resourceType,
        int resourceId = 0,
        object? before = null,
        object? after = null)
    {
        if (userId == null)
        {
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId.Value,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ChangesJson = JsonSerializer.Serialize(new { before, after }, JsonOptions),
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
