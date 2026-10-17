using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/search")]
    public class SearchController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Search(string q, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new { projects = new object[0], tasks = new object[0], users = new object[0] });

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();
            var searchTerm = q.Trim();

            var projects = await db.Projects
                .Where(p => p.CompanyId == companyId && p.ProjectName.Contains(searchTerm))
                .Take(limit)
                .Select(p => new { p.ProjectId, p.ProjectName, p.Status })
                .ToListAsync();

            var tasks = await db.Tasks
                .Where(t => t.Project.CompanyId == companyId && t.TaskName.Contains(searchTerm))
                .Take(limit)
                .Select(t => new { t.TaskId, t.TaskName, t.Status, t.ProjectId })
                .ToListAsync();

            var users = await db.Users
                .Where(u => u.CompanyId == companyId && u.IsActive &&
                    (u.FirstName.Contains(searchTerm) || u.LastName.Contains(searchTerm) || u.Email.Contains(searchTerm)))
                .Take(limit)
                .Select(u => new { u.UserId, u.FirstName, u.LastName, u.Email, u.Role })
                .ToListAsync();

            return Ok(new { projects, tasks, users });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}