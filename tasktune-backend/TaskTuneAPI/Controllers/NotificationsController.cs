using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/notifications")]
    public class NotificationsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> GetAll(int page = 1, int pageSize = 25, bool? isRead = null)
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();

            var query = db.Notifications.Where(n => n.UserId == userId);
            if (isRead.HasValue) query = query.Where(n => n.IsRead == isRead.Value);

            var total = await query.CountAsync();
            var items = await query.OrderByDescending(n => n.SentAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new PagedResultDto<NotificationResponseDto>
            {
                Items = items.Select(MapNotification).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet, Route("unread-count")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> UnreadCount()
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Ok(new { count });
        }

        [HttpPut, Route("{id:int}/mark-read")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> MarkRead(int id)
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var notification = await db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null) return NotFound();

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(MapNotification(notification));
        }

        [HttpPost, Route("mark-all-read")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> MarkAllRead()
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var unread = await db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead).ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }

            await db.SaveChangesAsync();
            return Ok(new MessageResponseDto { Message = $"{unread.Count} notifications marked as read" });
        }

        [HttpDelete, Route("{id:int}")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var userId = ((System.Security.Claims.ClaimsPrincipal)User).GetUserId();
            var notification = await db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification == null) return NotFound();

            db.Notifications.Remove(notification);
            await db.SaveChangesAsync();

            return StatusCode(System.Net.HttpStatusCode.NoContent);
        }

        private NotificationResponseDto MapNotification(Notification n)
        {
            return new NotificationResponseDto
            {
                NotificationId = n.NotificationId,
                NotificationType = n.NotificationType,
                Title = n.Title,
                Message = n.Message,
                RelatedTaskId = n.RelatedTaskId,
                RelatedProjectId = n.RelatedProjectId,
                IsRead = n.IsRead,
                SentAt = n.SentAt,
                ReadAt = n.ReadAt
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}