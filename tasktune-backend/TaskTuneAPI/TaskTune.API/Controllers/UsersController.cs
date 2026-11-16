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
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { message = "Page must be positive and pageSize must be between 1 and 100." });

        var query = _db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId.Value && u.IsActive);
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

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId.Value && u.UserId == id)
            .Include(u => u.Department)
            .Include(u => u.Position)
            .FirstOrDefaultAsync();
        if (user == null) return NotFound();

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
            Department = user.Department?.DepartmentName,
            Position = user.Position?.PositionTitle
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

        if (await _db.Users.AnyAsync(u => u.Email == dto.Email.Trim()))
            return Conflict(new { message = "Email already exists." });
        if (!await ReferencesBelongToCompanyAsync(companyId.Value, dto.DepartmentId, dto.PositionId))
            return BadRequest(new { message = "Department or position does not belong to this company." });

        // Tenant ownership always comes from the authenticated token, never the request DTO.
        var user = new User
        {
            CompanyId = companyId.Value,
            Email = dto.Email.Trim().ToLowerInvariant(),
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
