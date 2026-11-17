using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Application.DTOs.Users;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    public UsersController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Roles = "Admin,CEO,HR")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Users.Where(u => u.IsActive);
        var total = await query.CountAsync();
        var items = await query.OrderBy(u => u.UserId)
            .Include(u => u.Department).Include(u => u.Position)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.UserId, u.FirstName, u.LastName,
                FullName = u.FullName ?? (u.FirstName + " " + u.LastName),
                u.Email, u.Role, u.IsActive, u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.DepartmentName : null,
                u.PositionId, PositionTitle = u.Position != null ? u.Position.PositionTitle : null
            }).ToListAsync();
        return Ok(new { items, totalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null) return Unauthorized();
        if (id != currentUserId.Value &&
            !new[] { "Admin", "CEO", "HR" }.Contains(GetCurrentRole(), StringComparer.OrdinalIgnoreCase))
            return Forbid();

        var user = await _db.Users.Include(u => u.Department).Include(u => u.Position)
            .FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();

        return Ok(new
        {
            user.UserId, user.FirstName, user.LastName,
            FullName = user.FullName ?? (user.FirstName + " " + user.LastName),
            user.Email, user.Phone, user.Role, user.IsActive,
            Status = user.IsActive ? "Active" : "Inactive",
            Department = user.Department?.DepartmentName,
            Position = user.Position?.PositionTitle
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var actorId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (actorId == null || companyId == null) return Unauthorized();
        if (dto.Role > 5) return BadRequest(new { message = "Invalid role." });
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password) ||
            dto.Password.Length < 12 || string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName))
            return BadRequest(new { message = "Email, names and a password of at least 12 characters are required." });

        // Email is unique across the installation; use a generic conflict response to avoid
        // disclosing which tenant already owns the address.
        var normalizedEmail = dto.Email.Trim();
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail))
            return Conflict(new { message = "Email already exists." });

        if (!await ReferencesBelongToTenantAsync(companyId.Value, dto.DepartmentId, dto.PositionId))
            return BadRequest(new { message = "Department and position must belong to your company and match." });

        var user = new User
        {
            CompanyId = companyId.Value, // Client CompanyId is never trusted.
            Email = normalizedEmail,
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
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, action: "CREATE",
            resourceType: "User", resourceId: user.UserId,
            after: new { user.UserId, user.Email, user.Role, user.DepartmentId, user.PositionId });

        return CreatedAtAction(nameof(GetUser), new { id = user.UserId },
            new { user.UserId, user.Email, user.Role });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,CEO,HR")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        var actorId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (actorId == null || companyId == null) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();
        if (!await ReferencesBelongToTenantAsync(companyId.Value, dto.DepartmentId, dto.PositionId))
            return BadRequest(new { message = "Department and position must belong to your company and match." });

        var before = new { user.FirstName, user.LastName, user.Phone, user.DepartmentId, user.PositionId };
        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Phone = dto.Phone;
        user.DepartmentId = dto.DepartmentId;
        user.PositionId = dto.PositionId;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, action: "UPDATE",
            resourceType: "User", resourceId: user.UserId, before: before,
            after: new { user.FirstName, user.LastName, user.Phone, user.DepartmentId, user.PositionId });

        return Ok(new { user.UserId, user.FirstName, user.LastName });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var actorId = GetCurrentUserId();
        if (actorId == null) return Unauthorized();
        if (id == actorId.Value) return BadRequest(new { message = "You cannot deactivate your own account." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();
        var before = new { user.IsActive };
        user.IsActive = false;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, action: "DEACTIVATE",
            resourceType: "User", resourceId: user.UserId, before: before, after: new { user.IsActive });
        return NoContent();
    }

    private async Task<bool> ReferencesBelongToTenantAsync(int companyId, int? departmentId, int? positionId)
    {
        if (departmentId.HasValue &&
            !await _db.Departments.AnyAsync(d => d.DepartmentId == departmentId.Value && d.CompanyId == companyId))
            return false;
        if (positionId.HasValue)
        {
            var position = await _db.Positions.FirstOrDefaultAsync(p =>
                p.PositionId == positionId.Value && p.CompanyId == companyId);
            if (position == null || (departmentId.HasValue && position.DepartmentId != departmentId.Value))
                return false;
        }
        return true;
    }
}
