using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public NotificationsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var items = await _db.Notifications
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.SentAt)
            .Select(n => new
            {
                n.NotificationId,
                n.NotificationType,
                n.Title,
                n.Message,
                n.RelatedTaskId,
                n.RelatedProjectId,
                n.IsRead,
                n.SentAt,
                n.ReadAt
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPut("{id:int}/mark-read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId.Value);
        if (n == null) return NotFound();
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Marked as read." });
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _db.Notifications
            .Where(n => n.UserId == userId.Value && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return Ok(new { message = "All marked as read." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId.Value);
        if (n == null) return NotFound();
        _db.Notifications.Remove(n);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}