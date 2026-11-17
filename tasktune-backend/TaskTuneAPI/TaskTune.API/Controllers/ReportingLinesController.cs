using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/reporting-lines")]
[Authorize]
public sealed class ReportingLinesController : BaseApiController
{
    private static readonly HashSet<string> RelationshipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "line", "matrix", "project", "mentor"
    };

    private readonly TaskTuneDbContext _db;

    public ReportingLinesController(TaskTuneDbContext db) => _db = db;

    [HttpGet("employee/{employeeUserId:int}")]
    public async Task<IActionResult> GetEmployeeReportingLines(int employeeUserId)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        if (!await CanViewEmployeeAsync(_db, employeeUserId)) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await (
            from line in _db.EmployeeReportingLines.AsNoTracking()
            join manager in _db.Users.AsNoTracking() on line.ManagerUserId equals manager.UserId
            where line.CompanyId == companyId.Value &&
                  line.EmployeeUserId == employeeUserId &&
                  manager.CompanyId == companyId.Value &&
                  line.EffectiveFrom <= today &&
                  (!line.EffectiveTo.HasValue || line.EffectiveTo >= today)
            orderby line.IsPrimary descending, line.RelationshipType
            select new
            {
                line.ReportingLineId,
                line.EmployeeUserId,
                line.ManagerUserId,
                ManagerName = manager.FullName ?? (manager.FirstName + " " + manager.LastName),
                ManagerEmail = manager.Email,
                RelationshipType = line.RelationshipType,
                line.IsPrimary,
                line.EffectiveFrom,
                line.EffectiveTo
            })
            .ToListAsync();

        return Ok(new { employeeUserId, items });
    }

    [HttpGet("me/direct-reports")]
    public async Task<IActionResult> GetMyDirectReports()
    {
        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await (
            from line in _db.EmployeeReportingLines.AsNoTracking()
            join employee in _db.Users.AsNoTracking() on line.EmployeeUserId equals employee.UserId
            where line.CompanyId == companyId.Value &&
                  line.ManagerUserId == userId.Value &&
                  line.IsPrimary &&
                  line.RelationshipType == "line" &&
                  line.EffectiveFrom <= today &&
                  (!line.EffectiveTo.HasValue || line.EffectiveTo >= today) &&
                  employee.CompanyId == companyId.Value &&
                  employee.IsActive
            orderby employee.LastName, employee.FirstName
            select new
            {
                employee.UserId,
                FullName = employee.FullName ?? (employee.FirstName + " " + employee.LastName),
                employee.Email,
                employee.DepartmentId,
                DepartmentName = employee.Department != null ? employee.Department.DepartmentName : null,
                employee.PositionId,
                PositionTitle = employee.Position != null ? employee.Position.PositionTitle : null
            })
            .ToListAsync();

        return Ok(new { items, totalCount = items.Count });
    }

    [HttpPost]
    [Authorize(Roles = "HR,Manager,CEO,Admin")]
    public async Task<IActionResult> CreateReportingLine([FromBody] CreateReportingLineRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (!RelationshipTypes.Contains(request.RelationshipType ?? "line"))
            return BadRequest(new { message = "Unsupported reporting relationship type." });
        if (request.EmployeeUserId == request.ManagerUserId)
            return BadRequest(new { message = "An employee cannot report to themselves." });
        if (request.IsPrimary && !string.Equals(request.RelationshipType, "line", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only a line relationship can be the primary reporting relationship." });

        var employee = await _db.Users.FirstOrDefaultAsync(u =>
            u.UserId == request.EmployeeUserId && u.CompanyId == companyId.Value && u.IsActive);
        var manager = await _db.Users.FirstOrDefaultAsync(u =>
            u.UserId == request.ManagerUserId && u.CompanyId == companyId.Value && u.IsActive);
        if (employee == null || manager == null)
            return BadRequest(new { message = "Both employees must be active members of the same company." });

        var effectiveFrom = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.IsPrimary && await WouldCreateCycleAsync(companyId.Value, request.EmployeeUserId, request.ManagerUserId, effectiveFrom))
            return Conflict(new { message = "This reporting relationship would create a circular management hierarchy." });

        var overlapping = await _db.EmployeeReportingLines.AnyAsync(x =>
            x.CompanyId == companyId.Value &&
            x.EmployeeUserId == request.EmployeeUserId &&
            x.RelationshipType == request.RelationshipType &&
            x.IsPrimary == request.IsPrimary &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveFrom) &&
            x.EffectiveFrom <= (request.EffectiveTo ?? DateOnly.MaxValue));
        if (overlapping)
            return Conflict(new { message = "An overlapping reporting relationship already exists for this employee and relationship type." });

        var line = new EmployeeReportingLine
        {
            CompanyId = companyId.Value,
            EmployeeUserId = request.EmployeeUserId,
            ManagerUserId = request.ManagerUserId,
            RelationshipType = request.RelationshipType.Trim().ToLowerInvariant(),
            IsPrimary = request.IsPrimary,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedBy = actorId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _db.EmployeeReportingLines.Add(line);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "EmployeeReportingLine", line.ReportingLineId,
            after: new
            {
                line.EmployeeUserId, line.ManagerUserId, line.RelationshipType,
                line.IsPrimary, line.EffectiveFrom, line.EffectiveTo
            });

        return CreatedAtAction(
            nameof(GetEmployeeReportingLines),
            new { employeeUserId = line.EmployeeUserId },
            new { line.ReportingLineId, line.EmployeeUserId, line.ManagerUserId, line.RelationshipType, line.IsPrimary, line.EffectiveFrom, line.EffectiveTo });
    }

    [HttpPost("{reportingLineId:int}/close")]
    [Authorize(Roles = "HR,Manager,CEO,Admin")]
    public async Task<IActionResult> CloseReportingLine(int reportingLineId, [FromBody] CloseReportingLineRequest? request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        var line = await _db.EmployeeReportingLines.FirstOrDefaultAsync(x =>
            x.ReportingLineId == reportingLineId && x.CompanyId == companyId.Value);
        if (line == null) return NotFound();

        var closeDate = request?.EffectiveTo ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (closeDate < line.EffectiveFrom)
            return BadRequest(new { message = "The close date cannot be earlier than the effective start date." });
        if (line.EffectiveTo.HasValue && line.EffectiveTo.Value <= closeDate)
            return Conflict(new { message = "The reporting relationship is already closed." });

        var before = new { line.EffectiveTo };
        line.EffectiveTo = closeDate;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CLOSE", "EmployeeReportingLine", line.ReportingLineId,
            before: before, after: new { line.EffectiveTo });
        return Ok(new { line.ReportingLineId, line.EffectiveTo });
    }

    private async Task<bool> WouldCreateCycleAsync(int companyId, int employeeId, int managerId, DateOnly effectiveFrom)
    {
        var lines = await _db.EmployeeReportingLines.AsNoTracking()
            .Where(x => x.CompanyId == companyId &&
                        x.IsPrimary &&
                        x.RelationshipType == "line" &&
                        x.EffectiveFrom <= effectiveFrom &&
                        (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveFrom) &&
                        x.EmployeeUserId != employeeId)
            .Select(x => new { x.EmployeeUserId, x.ManagerUserId })
            .ToListAsync();

        var parentByEmployee = lines
            .GroupBy(x => x.EmployeeUserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ManagerUserId).First().ManagerUserId);

        var visited = new HashSet<int>();
        var cursor = managerId;
        while (visited.Add(cursor))
        {
            if (cursor == employeeId) return true;
            if (!parentByEmployee.TryGetValue(cursor, out cursor)) return false;
        }
        return true;
    }
}

public sealed record CreateReportingLineRequest(
    int EmployeeUserId,
    int ManagerUserId,
    string RelationshipType,
    bool IsPrimary,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record CloseReportingLineRequest(DateOnly? EffectiveTo);
