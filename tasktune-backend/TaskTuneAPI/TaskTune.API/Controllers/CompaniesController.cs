using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompaniesController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public CompaniesController(TaskTuneDbContext db) => _db = db;

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentCompany()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var company = await _db.Companies
            .Where(c => c.CompanyId == companyId.Value)
            .Select(c => new
            {
                c.CompanyId,
                c.CompanyName,
                c.SettingsJson,
                c.CreatedAt,
                c.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (company == null) return NotFound();
        return Ok(company);
    }

    [HttpPut("current")]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> UpdateCurrentCompany([FromBody] UpdateCompanyDto dto)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId.Value);
        if (company == null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.CompanyName) || dto.CompanyName.Trim().Length > 150 ||
            (dto.SettingsJson != null && dto.SettingsJson.Length > 20000))
            return BadRequest(new { message = "Company name or settings payload is invalid." });

        company.CompanyName = dto.CompanyName.Trim();
        company.SettingsJson = dto.SettingsJson;
        company.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { company.CompanyId, company.CompanyName, company.SettingsJson });
    }
}

public record UpdateCompanyDto(string CompanyName, string? SettingsJson);
