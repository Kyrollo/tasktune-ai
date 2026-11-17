using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Application.DTOs.Users;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireModule("core_hr")]
[Authorize]
public class UsersController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly TenantEntitlementService _entitlements;

    public UsersController(TaskTuneDbContext db, TenantEntitlementService entitlements)
    {
        _db = db;
        _entitlements = entitlements;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var companyId = GetCurrentCompanyId();
        var currentUserId = GetCurrentUserId();
        var role = GetCurrentRole();
        if (companyId == null || currentUserId == null || role == null) return Unauthorized();
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "Page must be positive and pageSize must be between 1 and 100." });

        var query = _db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId.Value && u.IsActive);

        if (role.Equals("Employee", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(u => u.UserId == currentUserId.Value);
        }
        else if (role.Equals("Supervisor", StringComparison.OrdinalIgnoreCase))
        {
            var departmentId = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == currentUserId.Value && u.CompanyId == companyId.Value)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync();
            query = departmentId.HasValue
                ? query.Where(u => u.DepartmentId == departmentId.Value || u.UserId == currentUserId.Value)
                : query.Where(u => u.UserId == currentUserId.Value);
        }
        else if (!new[] { "Manager", "HR", "CEO", "Admin" }.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var total = await query.CountAsync();
        var users = await query
            .Include(u => u.Department)
            .Include(u => u.Position)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.UserId,
                u.FirstName,
                u.LastName,
                FullName = u.FullName ?? (u.FirstName + " " + u.LastName),
                u.Email,
                u.Role,
                u.IsActive,
                u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.DepartmentName : null,
                u.PositionId,
                PositionTitle = u.Position != null ? u.Position.PositionTitle : null
            })
            .ToListAsync();

        return Ok(new { items = users, totalCount = total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        if (!await CanViewEmployeeAsync(_db, id)) return NotFound();

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId.Value && u.UserId == id)
            .Include(u => u.Department)
            .Include(u => u.Position)
            .FirstOrDefaultAsync();
        if (user == null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var manager = await (
            from line in _db.EmployeeReportingLines.AsNoTracking()
            join person in _db.Users.AsNoTracking() on line.ManagerUserId equals person.UserId
            where line.CompanyId == companyId.Value &&
                  line.EmployeeUserId == id &&
                  line.IsPrimary &&
                  line.RelationshipType == "line" &&
                  line.EffectiveFrom <= today &&
                  (!line.EffectiveTo.HasValue || line.EffectiveTo >= today) &&
                  person.CompanyId == companyId.Value &&
                  person.IsActive
            select new
            {
                ReportingLineId = line.ReportingLineId,
                UserId = person.UserId,
                FullName = person.FullName ?? (person.FirstName + " " + person.LastName),
                person.Email,
                Position = person.Position != null ? person.Position.PositionTitle : null
            })
            .FirstOrDefaultAsync();

        var directReports = await (
            from line in _db.EmployeeReportingLines.AsNoTracking()
            join person in _db.Users.AsNoTracking() on line.EmployeeUserId equals person.UserId
            where line.CompanyId == companyId.Value &&
                  line.ManagerUserId == id &&
                  line.IsPrimary &&
                  line.RelationshipType == "line" &&
                  line.EffectiveFrom <= today &&
                  (!line.EffectiveTo.HasValue || line.EffectiveTo >= today) &&
                  person.CompanyId == companyId.Value &&
                  person.IsActive
            orderby person.LastName, person.FirstName
            select new
            {
                UserId = person.UserId,
                FullName = person.FullName ?? (person.FirstName + " " + person.LastName),
                Position = person.Position != null ? person.Position.PositionTitle : null,
                Department = person.Department != null ? person.Department.DepartmentName : null
            })
            .ToListAsync();

        return Ok(new
        {
            user.UserId,
            user.FirstName,
            user.LastName,
            FullName = user.FullName ?? (user.FirstName + " " + user.LastName),
            user.Email,
            user.Phone,
            user.Role,
            user.IsActive,
            Status = user.IsActive ? "Active" : "Inactive",
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLogin,
            DepartmentId = user.DepartmentId,
            Department = user.Department?.DepartmentName,
            PositionId = user.PositionId,
            Position = user.Position?.PositionTitle,
            Manager = manager,
            DirectReports = directReports,
            DirectReportCount = directReports.Count
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,CEO")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var companyId = GetCurrentCompanyId();
        var currentUserId = GetCurrentUserId();
        if (companyId == null || currentUserId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "Email and password are required." });
        if (dto.Password.Length < 12)
            return BadRequest(new { message = "Password must be at least 12 characters." });
        if (dto.Role > 4)
            return BadRequest(new { message = "Tenant administrators cannot create platform-admin accounts." });

        try
        {
            await _entitlements.EnsureModuleEnabledAsync(companyId.Value, "core_hr");
            await _entitlements.EnsureCanAddEmployeeAsync(companyId.Value);
        }
        catch (TenantEntitlementException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message, traceId = HttpContext.TraceIdentifier });
        }

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email already exists." });
        if (!await ReferencesBelongToCompanyAsync(companyId.Value, dto.DepartmentId, dto.PositionId))
            return BadRequest(new { message = "Department or position does not belong to this company." });

        var user = new User
        {
            CompanyId = companyId.Value,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Role = dto.Role,
            DepartmentId = dto.DepartmentId,
            PositionId = dto.PositionId,
            Phone = dto.Phone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, currentUserId, "CREATE", "User", user.UserId,
            after: new { user.UserId, user.Email, user.Role, user.DepartmentId, user.PositionId });

        return CreatedAtAction(nameof(GetUser), new { id = user.UserId },
            new { user.UserId, user.Email, user.Role });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        var companyId = GetCurrentCompanyId();
        var currentUserId = GetCurrentUserId();
        if (companyId == null || currentUserId == null) return Unauthorized();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.CompanyId == companyId.Value);
        if (user == null) return NotFound();
        if (!await ReferencesBelongToCompanyAsync(companyId.Value, dto.DepartmentId, dto.PositionId))
            return BadRequest(new { message = "Department or position does not belong to this company." });

        if (dto.PositionId.HasValue && dto.DepartmentId.HasValue &&
            !await _db.Positions.AnyAsync(p => p.PositionId == dto.PositionId.Value &&
                                               p.DepartmentId == dto.DepartmentId.Value &&
                                               p.CompanyId == companyId.Value))
            return BadRequest(new { message = "The selected position does not belong to the selected department." });

        var before = new { user.FirstName, user.LastName, user.Phone, user.DepartmentId, user.PositionId };
        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Phone = dto.Phone;
        user.DepartmentId = dto.DepartmentId;
        user.PositionId = dto.PositionId;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, currentUserId, "UPDATE", "User", user.UserId,
            before: before,
            after: new { user.FirstName, user.LastName, user.Phone, user.DepartmentId, user.PositionId });
        return Ok(new { user.UserId, user.FirstName, user.LastName });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,CEO")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var companyId = GetCurrentCompanyId();
        var currentUserId = GetCurrentUserId();
        if (companyId == null || currentUserId == null) return Unauthorized();
        if (id == currentUserId.Value)
            return BadRequest(new { message = "You cannot deactivate your own account." });

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.CompanyId == companyId.Value);
        if (user == null) return NotFound();

        var before = new { user.IsActive };
        user.IsActive = false;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeLines = await _db.EmployeeReportingLines
            .Where(x => x.CompanyId == companyId.Value &&
                        (x.EmployeeUserId == id || x.ManagerUserId == id) &&
                        (!x.EffectiveTo.HasValue || x.EffectiveTo >= today))
            .ToListAsync();
        foreach (var line in activeLines)
            line.EffectiveTo = today;

        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, currentUserId, "DEACTIVATE", "User", user.UserId,
            before: before, after: new { user.IsActive });
        return NoContent();
    }

    private async Task<bool> ReferencesBelongToCompanyAsync(int companyId, int? departmentId, int? positionId)
    {
        if (departmentId.HasValue &&
            !await _db.Departments.AnyAsync(d => d.DepartmentId == departmentId.Value && d.CompanyId == companyId))
            return false;

        if (positionId.HasValue &&
            !await _db.Positions.AnyAsync(p => p.PositionId == positionId.Value && p.CompanyId == companyId))
            return false;

        return true;
    }
}
