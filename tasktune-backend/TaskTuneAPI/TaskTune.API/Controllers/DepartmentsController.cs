using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public DepartmentsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetDepartments()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var items = await _db.Departments
            .Where(d => d.CompanyId == companyId.Value)
            .Select(d => new
            {
                d.DepartmentId,
                d.DepartmentName,
                d.Description,
                d.CreatedAt,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDepartment(int id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var dept = await _db.Departments
            .Where(d => d.DepartmentId == id && d.CompanyId == companyId.Value)
            .Select(d => new
            {
                d.DepartmentId,
                d.CompanyId,
                d.DepartmentName,
                d.Description,
                d.CreatedAt,
                d.UpdatedAt,
                UsersCount = d.Users.Count
            })
            .FirstOrDefaultAsync();

        if (dept == null) return NotFound();
        return Ok(dept);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var dept = new Department
        {
            CompanyId = companyId.Value,
            DepartmentName = dto.DepartmentName,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetDepartment), new { id = dept.DepartmentId }, new
        {
            dept.DepartmentId,
            dept.DepartmentName,
            dept.Description
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpdateDepartmentDto dto)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var dept = await _db.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == id && d.CompanyId == companyId.Value);
        if (dept == null) return NotFound();

        dept.DepartmentName = dto.DepartmentName;
        dept.Description = dto.Description;
        dept.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { dept.DepartmentId, dept.DepartmentName, dept.Description });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var dept = await _db.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == id && d.CompanyId == companyId.Value);
        if (dept == null) return NotFound();

        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateDepartmentDto(string DepartmentName, string? Description);
public record UpdateDepartmentDto(string DepartmentName, string? Description);
