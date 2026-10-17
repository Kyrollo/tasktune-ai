using System;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;
using TaskTuneAPI.Models.DTOs;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/audit-logs")]
    public class AuditLogsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize(RoleNames.Manager, RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> GetAll(
            int page = 1, int pageSize = 25,
            string action = null, string resourceType = null,
            int? userId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = db.AuditLogs.Include(a => a.User).AsQueryable();

            // Admin sees all; others see only their company
            var principal = (System.Security.Claims.ClaimsPrincipal)User;
            var role = principal.GetRole();
            if (role != RoleNames.Admin)
            {
                var companyId = principal.GetCompanyId();
                query = query.Where(a => a.User.CompanyId == companyId);
            }

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action.Contains(action));
            if (!string.IsNullOrEmpty(resourceType))
                query = query.Where(a => a.ResourceType.Contains(resourceType));
            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);
            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PagedResultDto<AuditLogResponseDto>
            {
                Items = items.Select(MapAuditLog).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet, Route("export")]
        [JwtAuthorize(RoleNames.Admin)]
        public async Task<HttpResponseMessage> Export(
            string action = null, string resourceType = null,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = db.AuditLogs.Include(a => a.User).AsQueryable();

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action.Contains(action));
            if (!string.IsNullOrEmpty(resourceType))
                query = query.Where(a => a.ResourceType.Contains(resourceType));
            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            var items = await query.OrderByDescending(a => a.Timestamp).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("LogId,User,Action,ResourceType,ResourceId,IpAddress,Timestamp");
            foreach (var a in items)
            {
                var userName = a.User != null ? (a.User.FullName ?? (a.User.FirstName + " " + a.User.LastName)) : "";
                sb.AppendLine($"{a.LogId},\"{userName}\",\"{a.Action}\",\"{a.ResourceType}\",{a.ResourceId},\"{a.IpAddress}\",\"{a.Timestamp:yyyy-MM-dd HH:mm:ss}\"");
            }

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(sb.ToString(), Encoding.UTF8, "text/csv")
            };
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"
            };

            return response;
        }

        private AuditLogResponseDto MapAuditLog(AuditLog a)
        {
            return new AuditLogResponseDto
            {
                LogId = a.LogId,
                UserName = a.User != null ? (a.User.FullName ?? (a.User.FirstName + " " + a.User.LastName)) : null,
                Action = a.Action,
                ResourceType = a.ResourceType,
                ResourceId = a.ResourceId,
                ChangesJson = a.ChangesJson,
                IpAddress = a.IpAddress,
                Timestamp = a.Timestamp
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}