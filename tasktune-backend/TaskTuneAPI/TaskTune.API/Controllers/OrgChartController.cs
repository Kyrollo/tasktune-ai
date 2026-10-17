using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/org-chart")]
[Authorize]
public class OrgChartController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public OrgChartController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetOrgChart()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var company = await _db.Companies
            .Where(c => c.CompanyId == companyId.Value)
            .Select(c => new
            {
                c.CompanyId,
                c.CompanyName
            })
            .FirstOrDefaultAsync();

        var users = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.Position)
            .Where(u => u.CompanyId == companyId.Value && u.IsActive)
            .ToListAsync();

        var roleNames = new Dictionary<byte, string>
        {
            { 0, "Employee" },
            { 1, "Supervisor" },
            { 2, "Manager" },
            { 3, "HR" },
            { 4, "CEO" },
            { 5, "Admin" }
        };

        var ceo = users.FirstOrDefault(u => u.Role == 4);
        var ceoNode = ceo != null ? new
        {
            UserId = ceo.UserId,
            FullName = ceo.FullName ?? $"{ceo.FirstName} {ceo.LastName}",
            Email = ceo.Email,
            Position = ceo.Position?.PositionTitle ?? "CEO",
            Role = ceo.Role,
            RoleName = roleNames.GetValueOrDefault(ceo.Role, "CEO"),
            DepartmentName = ceo.Department?.DepartmentName ?? "Executive"
        } : (object?)null;

        var departmentRows = await _db.Departments
            .Where(d => d.CompanyId == companyId.Value)
            .OrderBy(d => d.DepartmentName)
            .Select(d => new
            {
                d.DepartmentId,
                d.DepartmentName,
                d.Description,
                d.ParentDepartmentId
            })
            .ToListAsync();

        var positions = await _db.Positions
            .Where(p => p.CompanyId == companyId.Value)
            .OrderBy(p => p.PositionTitle)
            .Select(p => new
            {
                p.PositionId,
                p.DepartmentId,
                p.PositionTitle
            })
            .ToListAsync();

        var departments = departmentRows.Select(d => new
        {
            d.DepartmentId,
            d.DepartmentName,
            d.Description,
            d.ParentDepartmentId,
            EmployeeCount = users.Count(u => u.DepartmentId == d.DepartmentId),
            Members = users
                .Where(u => u.DepartmentId == d.DepartmentId)
                .OrderByDescending(u => u.Role)
                .ThenBy(u => u.FullName)
                .Select(u => new
                {
                    u.UserId,
                    FullName = u.FullName ?? $"{u.FirstName} {u.LastName}",
                    u.Email,
                    Position = u.Position != null ? u.Position.PositionTitle : null,
                    Role = u.Role,
                    RoleName = roleNames.GetValueOrDefault(u.Role, "Employee"),
                    DepartmentName = d.DepartmentName
                })
                .ToList(),
            Positions = positions
                .Where(p => p.DepartmentId == d.DepartmentId)
                .Select(p => new
                {
                    p.PositionId,
                    p.PositionTitle,
                    Users = users
                        .Where(u => u.PositionId == p.PositionId && u.DepartmentId == d.DepartmentId)
                        .OrderByDescending(u => u.Role)
                        .ThenBy(u => u.FullName)
                        .Select(u => new
                        {
                            u.UserId,
                            FullName = u.FullName ?? $"{u.FirstName} {u.LastName}",
                            u.Email,
                            Position = p.PositionTitle,
                            Role = u.Role,
                            RoleName = roleNames.GetValueOrDefault(u.Role, "Employee")
                        })
                        .ToList()
                })
                .ToList()
        }).ToList();

        return Ok(new
        {
            Company = company,
            CEO = ceoNode,
            Departments = departments
        });
    }
}
