using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/user-permissions")]
[Authorize(Roles = "Admin,CEO")]
public sealed class UserPermissionsController : BaseApiController
{
    private static readonly HashSet<string> AllowedResources = new(StringComparer.OrdinalIgnoreCase)
    {
        "employees", "tasks", "projects", "reports", "attendance", "leave",
        "payroll", "hr_cases", "documents", "org_chart", "permissions", "integrations"
    };

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "create", "update", "delete", "approve", "export", "manage"
    };

    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "self", "team", "department", "company", "selected"
    };

    private readonly TaskTuneDbContext _db;

    public UserPermissionsController(TaskTuneDbContext db) => _db = db;

    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUserOverrides(int userId)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        if (!await _db.Users.AnyAsync(u => u.UserId == userId && u.CompanyId == companyId.Value))
            return NotFound();

        var items = await _db.UserPermissionOverrides.AsNoTracking()
            .Where(p => p.CompanyId == companyId.Value && p.UserId == userId)
            .OrderBy(p => p.Resource).ThenBy(p => p.Action).ThenBy(p => p.ScopeResourceId)
            .Select(p => new
            {
                p.PermissionOverrideId,
                p.UserId,
                p.Resource,
                p.Action,
                p.CanPerform,
                p.Scope,
                p.ScopeResourceId,
                p.ExpiresAt,
                p.Reason,
                p.GrantedBy,
                p.CreatedAt,
                IsExpired = p.ExpiresAt.HasValue && p.ExpiresAt <= DateTime.UtcNow
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPut("users/{userId:int}")]
    public async Task<IActionResult> UpsertUserOverrides(int userId, [FromBody] UpsertUserPermissionsRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (request.Permissions == null || request.Permissions.Count == 0 || request.Permissions.Count > 100)
            return BadRequest(new { message = "Provide between 1 and 100 permission overrides." });

        var targetUser = await _db.Users.FirstOrDefaultAsync(
            u => u.UserId == userId && u.CompanyId == companyId.Value && u.IsActive);
        if (targetUser == null) return NotFound();

        var requestedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Permissions)
        {
            if (!AllowedResources.Contains(item.Resource ?? "") ||
                !AllowedActions.Contains(item.Action ?? "") ||
                !AllowedScopes.Contains(item.Scope ?? ""))
                return BadRequest(new { message = "A permission contains an unsupported resource, action, or scope." });

            if (item.ScopeResourceId.HasValue &&
                !await _db.Users.AnyAsync(u => u.UserId == item.ScopeResourceId.Value &&
                                              u.CompanyId == companyId.Value && u.IsActive))
                return BadRequest(new { message = "The selected scope resource does not belong to this company." });

            if (string.Equals(item.Scope, "selected", StringComparison.OrdinalIgnoreCase) != item.ScopeResourceId.HasValue)
                return BadRequest(new { message = "Selected scope requires exactly one target employee; other scopes must not specify a target employee." });

            if (item.ExpiresAt.HasValue && item.ExpiresAt.Value <= DateTime.UtcNow)
                return BadRequest(new { message = "Expiry must be in the future." });

            var key = $"{item.Resource.Trim().ToLowerInvariant()}|{item.Action.Trim().ToLowerInvariant()}|{item.ScopeResourceId?.ToString() ?? "all"}";
            if (!requestedKeys.Add(key))
                return BadRequest(new { message = "Duplicate resource/action/scope overrides were supplied." });
        }

        var existing = await _db.UserPermissionOverrides
            .Where(p => p.CompanyId == companyId.Value && p.UserId == userId)
            .ToListAsync();

        var before = existing.Select(p => new
        {
            p.PermissionOverrideId, p.Resource, p.Action, p.CanPerform, p.Scope, p.ScopeResourceId, p.ExpiresAt
        }).ToList();

        foreach (var item in request.Permissions)
        {
            var resource = item.Resource.Trim().ToLowerInvariant();
            var action = item.Action.Trim().ToLowerInvariant();
            var scope = item.Scope.Trim().ToLowerInvariant();
            var row = existing.FirstOrDefault(p =>
                p.Resource == resource && p.Action == action && p.ScopeResourceId == item.ScopeResourceId);

            if (row == null)
            {
                row = new UserPermissionOverride
                {
                    CompanyId = companyId.Value,
                    UserId = userId,
                    Resource = resource,
                    Action = action,
                    GrantedBy = actorId.Value,
                    CreatedAt = DateTime.UtcNow
                };
                _db.UserPermissionOverrides.Add(row);
                existing.Add(row);
            }

            row.CanPerform = item.CanPerform;
            row.Scope = scope;
            row.ScopeResourceId = item.ScopeResourceId;
            row.ExpiresAt = item.ExpiresAt;
            row.Reason = string.IsNullOrWhiteSpace(item.Reason) ? null : item.Reason.Trim();
            row.GrantedBy = actorId.Value;
        }

        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "UPSERT", "UserPermissionOverride", userId,
            before: before,
            after: request.Permissions.Select(p => new
            {
                p.Resource, p.Action, p.CanPerform, p.Scope, p.ScopeResourceId, p.ExpiresAt, p.Reason
            }).ToList());

        return Ok(new { message = "Individual permissions saved.", userId });
    }

    [HttpDelete("users/{userId:int}/{overrideId:int}")]
    public async Task<IActionResult> DeleteOverride(int userId, int overrideId)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        var row = await _db.UserPermissionOverrides.FirstOrDefaultAsync(p =>
            p.PermissionOverrideId == overrideId &&
            p.UserId == userId &&
            p.CompanyId == companyId.Value);
        if (row == null) return NotFound();

        var before = new { row.Resource, row.Action, row.CanPerform, row.Scope, row.ScopeResourceId, row.ExpiresAt };
        _db.UserPermissionOverrides.Remove(row);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "REVOKE", "UserPermissionOverride", overrideId,
            before: before, after: null);
        return NoContent();
    }
}

public sealed record UpsertUserPermissionsRequest(List<UserPermissionOverrideInput> Permissions);
public sealed record UserPermissionOverrideInput(
    string Resource,
    string Action,
    bool CanPerform,
    string Scope,
    int? ScopeResourceId,
    DateTime? ExpiresAt,
    string? Reason);
