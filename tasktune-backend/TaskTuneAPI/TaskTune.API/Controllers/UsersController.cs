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
        var query = _db.Users.Where(u => u.IsActive);
        var total = await query.CountAsync();
        var users = await query
            .Include(u => u.Department)
            .Include(u => u.Position)
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
        return Ok(new { items = users, totalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.Position)
            .FirstOrDefaultAsync(u => u.UserId == id);
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var currentUserId = GetCurrentUserId();
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return Conflict(new { message = "Email already exists." });

        var user = new User
        {
            CompanyId = dto.CompanyId,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
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
        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            currentUserId,
            action: "CREATE",
            resourceType: "User",
            resourceId: user.UserId,
            after: new
            {
                user.UserId,
                user.Email,
                user.Role,
                user.DepartmentId,
                user.PositionId
            });
        return CreatedAtAction(nameof(GetUser), new { id = user.UserId },
            new { user.UserId, user.Email, user.Role });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        var currentUserId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var before = new
        {
            user.FirstName,
            user.LastName,
            user.Phone,
            user.DepartmentId,
            user.PositionId
        };

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Phone = dto.Phone;
        user.DepartmentId = dto.DepartmentId;
        user.PositionId = dto.PositionId;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            currentUserId,
            action: "UPDATE",
            resourceType: "User",
            resourceId: user.UserId,
            before: before,
            after: new
            {
                user.FirstName,
                user.LastName,
                user.Phone,
                user.DepartmentId,
                user.PositionId
            });
        return Ok(new { user.UserId, user.FirstName, user.LastName });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var currentUserId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        var before = new { user.IsActive };
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(
            _db,
            HttpContext,
            currentUserId,
            action: "DEACTIVATE",
            resourceType: "User",
            resourceId: user.UserId,
            before: before,
            after: new { user.IsActive });
        return NoContent();
    }
}
