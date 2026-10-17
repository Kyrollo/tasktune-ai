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
    [RoutePrefix("api/role-permissions")]
    public class RolePermissionsController : ApiController
    {
        private readonly TaskTuneDbContext db = new TaskTuneDbContext();

        [HttpGet, Route("")]
        [JwtAuthorize(RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> GetAll()
        {
            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            var permissions = await db.RolePermissions
                .Where(r => r.CompanyId == companyId)
                .OrderBy(r => r.Role).ThenBy(r => r.Resource).ThenBy(r => r.Action)
                .ToListAsync();

            return Ok(permissions.Select(p => new RolePermissionDto
            {
                PermissionId = p.PermissionId,
                Role = p.Role,
                Resource = p.Resource,
                Action = p.Action,
                CanPerform = p.CanPerform
            }).ToList());
        }

        [HttpPut, Route("")]
        [JwtAuthorize(RoleNames.CEO, RoleNames.Admin)]
        public async Task<IHttpActionResult> Update(UpdateRolePermissionsDto dto)
        {
            if (dto == null || dto.Permissions == null)
                return BadRequest("Permissions list is required");

            var companyId = ((System.Security.Claims.ClaimsPrincipal)User).GetCompanyId();

            foreach (var item in dto.Permissions)
            {
                var existing = await db.RolePermissions
                    .FirstOrDefaultAsync(r =>
                        r.CompanyId == companyId &&
                        r.Role == item.Role &&
                        r.Resource == item.Resource &&
                        r.Action == item.Action);

                if (existing != null)
                {
                    existing.CanPerform = item.CanPerform;
                }
                else
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        CompanyId = companyId,
                        Role = item.Role,
                        Resource = item.Resource,
                        Action = item.Action,
                        CanPerform = item.CanPerform,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await db.SaveChangesAsync();
            return Ok(new MessageResponseDto { Message = $"{dto.Permissions.Count} permissions updated" });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}