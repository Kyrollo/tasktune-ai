using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

public abstract class BaseApiController : ControllerBase
{
    protected int? GetCurrentUserId()
    {
        // With MapInboundClaims=false, the claim type is "sub" not ClaimTypes.NameIdentifier
        var claim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                 ?? User.FindFirst("sub")?.Value
                 ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : null;
    }

    protected int? GetCurrentCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return int.TryParse(claim, out int id) ? id : null;
    }

    protected string? GetCurrentRole()
    {
        return User.FindFirst("role")?.Value;
    }

    protected byte? GetCurrentRoleByte()
    {
        return GetCurrentRole()?.ToLowerInvariant() switch
        {
            "employee" => 0,
            "supervisor" => 1,
            "manager" => 2,
            "hr" => 3,
            "ceo" => 4,
            "admin" => 5,
            _ => null
        };
    }

    protected async Task<bool> HasPageAccessAsync(TaskTuneDbContext db, string resource, params string[] privilegedRoles)
    {
        var currentRole = GetCurrentRole();
        if (!string.IsNullOrWhiteSpace(currentRole) &&
            privilegedRoles.Any(role => string.Equals(role, currentRole, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var companyId = GetCurrentCompanyId();
        var roleByte = GetCurrentRoleByte();
        if (companyId == null || roleByte == null)
        {
            return false;
        }

        return await db.RolePermissions.AnyAsync(rp =>
            rp.CompanyId == companyId.Value &&
            rp.Role == roleByte.Value &&
            rp.Resource == resource &&
            (rp.Action == "access" || rp.Action == "read") &&
            rp.CanPerform);
    }
}
