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



    protected async Task<bool> CanViewTaskAsync(TaskTuneDbContext db, int taskId)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        var role = GetCurrentRole();
        if (userId == null || companyId == null || string.IsNullOrWhiteSpace(role)) return false;

        var task = await db.Tasks.AsNoTracking()
            .Where(t => t.TaskId == taskId && t.Project.CompanyId == companyId.Value)
            .Select(t => new
            {
                t.AssignedTo,
                t.CreatedBy,
                AssignedDepartmentId = t.AssignedToNavigation != null ? t.AssignedToNavigation.DepartmentId : null
            })
            .FirstOrDefaultAsync();
        if (task == null) return false;

        if (role.Equals("Employee", StringComparison.OrdinalIgnoreCase))
            return task.AssignedTo == userId.Value || task.CreatedBy == userId.Value;

        if (role.Equals("Supervisor", StringComparison.OrdinalIgnoreCase))
        {
            var currentDepartmentId = await db.Users.AsNoTracking()
                .Where(u => u.UserId == userId.Value && u.CompanyId == companyId.Value)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync();

            return task.CreatedBy == userId.Value ||
                   task.AssignedTo == userId.Value ||
                   (currentDepartmentId.HasValue && task.AssignedDepartmentId == currentDepartmentId) ||
                   await IsInReportingChainAsync(db, companyId.Value, userId.Value, task.AssignedTo);
        }

        return role.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("CEO", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    }

    protected async Task<bool> CanViewProjectAsync(TaskTuneDbContext db, int projectId)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        var role = GetCurrentRole();
        if (userId == null || companyId == null || string.IsNullOrWhiteSpace(role)) return false;

        var project = await db.Projects.AsNoTracking()
            .Where(p => p.ProjectId == projectId && p.CompanyId == companyId.Value)
            .Select(p => new { p.ManagerId })
            .FirstOrDefaultAsync();
        if (project == null) return false;

        if (role.Equals("Manager", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("CEO", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        var taskIds = db.Tasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.Project.CompanyId == companyId.Value);

        if (role.Equals("Employee", StringComparison.OrdinalIgnoreCase))
            return project.ManagerId == userId.Value ||
                   await taskIds.AnyAsync(t => t.AssignedTo == userId.Value || t.CreatedBy == userId.Value);

        if (role.Equals("Supervisor", StringComparison.OrdinalIgnoreCase))
        {
            var departmentId = await db.Users.AsNoTracking()
                .Where(u => u.UserId == userId.Value && u.CompanyId == companyId.Value)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync();
            return project.ManagerId == userId.Value ||
                   await taskIds.AnyAsync(t => t.AssignedTo == userId.Value ||
                                               t.CreatedBy == userId.Value ||
                                               (departmentId.HasValue && t.AssignedToNavigation.DepartmentId == departmentId.Value));
        }

        return false;
    }

    protected async Task<bool> HasEffectivePermissionAsync(
        TaskTuneDbContext db, string resource, string action, int? scopeResourceId = null)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return false;

        var now = DateTime.UtcNow;
        var overrides = await db.UserPermissionOverrides.AsNoTracking()
            .Where(p => p.CompanyId == companyId.Value &&
                        p.UserId == userId.Value &&
                        p.Resource == resource &&
                        p.Action == action &&
                        (!p.ExpiresAt.HasValue || p.ExpiresAt > now) &&
                        (p.ScopeResourceId == null || p.ScopeResourceId == scopeResourceId))
            .OrderByDescending(p => p.ScopeResourceId.HasValue)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();

        var exact = scopeResourceId.HasValue
            ? overrides.FirstOrDefault(p => p.ScopeResourceId == scopeResourceId.Value)
            : null;
        if (exact != null) return exact.CanPerform;

        var broad = overrides.FirstOrDefault(p => p.ScopeResourceId == null);
        if (broad != null)
        {
            if (!broad.CanPerform) return false;
            return broad.Scope switch
            {
                "company" => true,
                "department" => await IsSameDepartmentAsync(db, companyId.Value, userId.Value, scopeResourceId),
                "team" => await IsInReportingChainAsync(db, companyId.Value, userId.Value, scopeResourceId),
                "self" => scopeResourceId == userId.Value,
                _ => false
            };
        }

        var roleByte = GetCurrentRoleByte();
        if (roleByte == null) return false;
        return await db.RolePermissions.AsNoTracking().AnyAsync(p =>
            p.CompanyId == companyId.Value &&
            p.Role == roleByte.Value &&
            p.Resource == resource &&
            p.Action == action &&
            p.CanPerform);
    }

    protected async Task<bool> CanViewEmployeeAsync(TaskTuneDbContext db, int targetUserId)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return false;
        if (targetUserId == userId.Value)
            return await db.Users.AnyAsync(u => u.UserId == targetUserId && u.CompanyId == companyId.Value && u.IsActive);

        var target = await db.Users.AsNoTracking()
            .Where(u => u.UserId == targetUserId && u.CompanyId == companyId.Value && u.IsActive)
            .Select(u => new { u.DepartmentId })
            .FirstOrDefaultAsync();
        if (target == null) return false;

        var effective = await HasEffectivePermissionAsync(db, "employees", "read", targetUserId);
        if (effective) return true;

        // A scoped explicit deny must override role-derived visibility.
        var now = DateTime.UtcNow;
        var explicitDenies = await db.UserPermissionOverrides.AsNoTracking()
            .Where(p => p.CompanyId == companyId.Value &&
                        p.UserId == userId.Value &&
                        p.Resource == "employees" &&
                        p.Action == "read" &&
                        !p.CanPerform &&
                        (!p.ExpiresAt.HasValue || p.ExpiresAt > now) &&
                        (p.ScopeResourceId == null || p.ScopeResourceId == targetUserId))
            .ToListAsync();

        if (explicitDenies.Any(p => p.ScopeResourceId == targetUserId))
            return false;

        foreach (var deny in explicitDenies.Where(p => p.ScopeResourceId == null))
        {
            var applies = deny.Scope switch
            {
                "company" => true,
                "department" => await IsSameDepartmentAsync(db, companyId.Value, userId.Value, targetUserId),
                "team" => await IsInReportingChainAsync(db, companyId.Value, userId.Value, targetUserId),
                "self" => targetUserId == userId.Value,
                _ => false
            };
            if (applies) return false;
        }

        var role = GetCurrentRole();
        if (string.Equals(role, "HR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "CEO", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(role, "Supervisor", StringComparison.OrdinalIgnoreCase))
        {
            var currentDepartmentId = await db.Users.AsNoTracking()
                .Where(u => u.UserId == userId.Value && u.CompanyId == companyId.Value)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync();
            if (currentDepartmentId.HasValue && currentDepartmentId == target.DepartmentId)
                return true;
            return await IsInReportingChainAsync(db, companyId.Value, userId.Value, targetUserId);
        }

        return false;
    }

    private static async Task<bool> IsSameDepartmentAsync(
        TaskTuneDbContext db, int companyId, int currentUserId, int? targetUserId)
    {
        if (targetUserId == null) return false;
        var departments = await db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId && (u.UserId == currentUserId || u.UserId == targetUserId.Value))
            .Select(u => new { u.UserId, u.DepartmentId })
            .ToListAsync();
        var own = departments.FirstOrDefault(u => u.UserId == currentUserId);
        var target = departments.FirstOrDefault(u => u.UserId == targetUserId.Value);
        return own?.DepartmentId != null && own.DepartmentId == target?.DepartmentId;
    }

    private static async Task<bool> IsInReportingChainAsync(
        TaskTuneDbContext db, int companyId, int managerUserId, int? employeeUserId)
    {
        if (employeeUserId == null) return false;
        var edges = await db.EmployeeReportingLines.AsNoTracking()
            .Where(x => x.CompanyId == companyId &&
                        x.IsPrimary &&
                        x.RelationshipType == "line" &&
                        x.EffectiveFrom <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                        (!x.EffectiveTo.HasValue || x.EffectiveTo >= DateOnly.FromDateTime(DateTime.UtcNow)))
            .Select(x => new { x.EmployeeUserId, x.ManagerUserId })
            .ToListAsync();

        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(managerUserId);
        while (queue.Count > 0 && visited.Count <= edges.Count + 1)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;
            foreach (var edge in edges.Where(e => e.ManagerUserId == current))
            {
                if (edge.EmployeeUserId == employeeUserId.Value) return true;
                if (!visited.Contains(edge.EmployeeUserId)) queue.Enqueue(edge.EmployeeUserId);
            }
        }
        return false;
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
