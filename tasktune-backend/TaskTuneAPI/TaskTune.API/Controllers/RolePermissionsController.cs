using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/role-permissions")]
[Authorize]
public class RolePermissionsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public RolePermissionsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetPermissions()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var permissions = await _db.RolePermissions
            .Where(rp => rp.CompanyId == companyId.Value)
            .OrderBy(rp => rp.Role)
            .ThenBy(rp => rp.Resource)
            .ThenBy(rp => rp.Action)
            .Select(rp => new
            {
                rp.PermissionId,
                rp.CompanyId,
                rp.Role,
                rp.Resource,
                rp.Action,
                rp.CanPerform,
                rp.CreatedAt
            })
            .ToListAsync();

        return Ok(permissions);
    }

    [HttpPut]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> UpdatePermissions([FromBody] UpdateRolePermissionsRequest request)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        foreach (var item in request.Permissions)
        {
            var existing = await _db.RolePermissions
                .FirstOrDefaultAsync(rp =>
                    rp.CompanyId == companyId.Value &&
                    rp.Role == item.Role &&
                    rp.Resource == item.Resource &&
                    rp.Action == item.Action);

            if (existing != null)
            {
                existing.CanPerform = item.CanPerform;
            }
            else
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    CompanyId = companyId.Value,
                    Role = item.Role,
                    Resource = item.Resource,
                    Action = item.Action,
                    CanPerform = item.CanPerform,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Permissions updated successfully." });
    }
}

public record UpdateRolePermissionsRequest(List<RolePermissionUpdateItem> Permissions);

public record RolePermissionUpdateItem(byte Role, string Resource, string Action, bool CanPerform);
