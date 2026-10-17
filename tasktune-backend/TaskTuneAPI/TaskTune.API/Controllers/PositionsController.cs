using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PositionsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public PositionsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetPositions()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var items = await _db.Positions
            .Where(p => p.CompanyId == companyId.Value)
            .Include(p => p.Department)
            .Select(p => new
            {
                p.PositionId,
                p.DepartmentId,
                p.PositionTitle,
                p.HierarchyLevel,
                p.Responsibilities,
                p.ReportsToPositionId,
                p.CreatedAt,
                p.UpdatedAt,
                DepartmentName = p.Department.DepartmentName
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPosition(int id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var pos = await _db.Positions
            .Where(p => p.PositionId == id && p.CompanyId == companyId.Value)
            .Include(p => p.Department)
            .Select(p => new
            {
                p.PositionId,
                p.CompanyId,
                p.DepartmentId,
                p.PositionTitle,
                p.HierarchyLevel,
                p.Responsibilities,
                p.ReportsToPositionId,
                p.CreatedAt,
                p.UpdatedAt,
                DepartmentName = p.Department.DepartmentName
            })
            .FirstOrDefaultAsync();

        if (pos == null) return NotFound();
        return Ok(pos);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreatePosition([FromBody] CreatePositionDto dto)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var pos = new Position
        {
            CompanyId = companyId.Value,
            DepartmentId = dto.DepartmentId,
            PositionTitle = dto.PositionTitle,
            HierarchyLevel = dto.HierarchyLevel,
            Responsibilities = dto.Responsibilities,
            ReportsToPositionId = dto.ReportsToPositionId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Positions.Add(pos);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPosition), new { id = pos.PositionId }, new
        {
            pos.PositionId,
            pos.PositionTitle,
            pos.DepartmentId,
            pos.HierarchyLevel
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePosition(int id, [FromBody] UpdatePositionDto dto)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var pos = await _db.Positions
            .FirstOrDefaultAsync(p => p.PositionId == id && p.CompanyId == companyId.Value);
        if (pos == null) return NotFound();

        pos.DepartmentId = dto.DepartmentId;
        pos.PositionTitle = dto.PositionTitle;
        pos.HierarchyLevel = dto.HierarchyLevel;
        pos.Responsibilities = dto.Responsibilities;
        pos.ReportsToPositionId = dto.ReportsToPositionId;
        pos.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { pos.PositionId, pos.PositionTitle, pos.DepartmentId });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePosition(int id)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var pos = await _db.Positions
            .FirstOrDefaultAsync(p => p.PositionId == id && p.CompanyId == companyId.Value);
        if (pos == null) return NotFound();

        _db.Positions.Remove(pos);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record CreatePositionDto(string PositionTitle, int DepartmentId, int HierarchyLevel, string? Responsibilities, int? ReportsToPositionId);
public record UpdatePositionDto(string PositionTitle, int DepartmentId, int HierarchyLevel, string? Responsibilities, int? ReportsToPositionId);
