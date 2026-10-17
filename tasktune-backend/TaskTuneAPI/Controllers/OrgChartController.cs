using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using TaskTuneAPI.Filters;
using TaskTuneAPI.Helpers;
using TaskTuneAPI.Models;

namespace TaskTuneAPI.Controllers
{
    [RoutePrefix("api/org-chart")]
    public class OrgChartController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize]
        public async Task<IHttpActionResult> Get()
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var departments = await db.Departments
                .Where(d => d.CompanyId == companyId)
                .OrderBy(d => d.DepartmentName)
                .ToListAsync();

            var users = await db.Users
                .Include(u => u.Position)
                .Where(u => u.CompanyId == companyId && u.IsActive)
                .OrderBy(u => u.Position != null ? u.Position.HierarchyLevel : 99)
                .ToListAsync();

            var ceo = users.FirstOrDefault(u => u.Role == RoleNames.CEO);

            var orgChart = new
            {
                company = await db.Companies.Where(c => c.CompanyId == companyId)
                    .Select(c => new { c.CompanyId, c.CompanyName }).FirstOrDefaultAsync(),
                ceo = ceo != null ? new
                {
                    ceo.UserId,
                    FullName = ceo.FullName ?? (ceo.FirstName + " " + ceo.LastName),
                    ceo.Email,
                    Position = ceo.Position?.PositionTitle,
                    ceo.Role
                } : null,
                departments = departments.Select(d => new
                {
                    d.DepartmentId,
                    d.DepartmentName,
                    d.Description,
                    d.ParentDepartmentId,
                    members = users.Where(u => u.DepartmentId == d.DepartmentId).Select(u => new
                    {
                        u.UserId,
                        FullName = u.FullName ?? (u.FirstName + " " + u.LastName),
                        u.Email,
                        Position = u.Position?.PositionTitle,
                        u.Role,
                        RoleName = RoleNames.GetName(u.Role)
                    }).ToList()
                }).ToList()
            };

            return Ok(orgChart);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}