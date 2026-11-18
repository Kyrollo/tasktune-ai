using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

/// <summary>
/// Cross-tenant tenant inventory and lifecycle operations. Access is granted exclusively
/// to the server-configured PlatformAdmin JWT claim; tenant roles cannot reach this controller.
/// </summary>
[ApiController]
[Route("api/platform/tenants")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformTenantsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly TenantLifecycleService _lifecycle;

    public PlatformTenantsController(TaskTuneDbContext db, TenantLifecycleService lifecycle)
    {
        _db = db;
        _lifecycle = lifecycle;
    }

    [HttpGet]
    public async Task<IActionResult> GetTenants(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Companies.IgnoreQueryFilters().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (normalized is not ("Active" or "Suspended"))
                return BadRequest(new { message = "Status must be Active or Suspended." });
            query = query.Where(company => company.Status == normalized);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(company => company.CompanyName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(company => company.CompanyId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(company => new
            {
                company.CompanyId,
                company.CompanyName,
                company.Status,
                company.CreatedAt,
                company.SuspendedAt,
                ActiveUserCount = _db.Users.IgnoreQueryFilters()
                    .Count(user => user.CompanyId == company.CompanyId && user.IsActive)
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items, totalCount = total, page, pageSize });
    }

    [HttpGet("{companyId:int}")]
    public async Task<IActionResult> GetTenant(int companyId, CancellationToken cancellationToken)
    {
        var company = await _db.Companies.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .Select(item => new
            {
                item.CompanyId,
                item.CompanyName,
                item.Status,
                item.CreatedAt,
                item.UpdatedAt,
                item.SuspendedAt,
                ActiveUserCount = _db.Users.IgnoreQueryFilters()
                    .Count(user => user.CompanyId == item.CompanyId && user.IsActive),
                TotalUserCount = _db.Users.IgnoreQueryFilters()
                    .Count(user => user.CompanyId == item.CompanyId)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (company == null) return NotFound();

        var lifecycle = await _db.TenantLifecycleEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new
            {
                item.EventId, item.ActorUserId, item.Action,
                item.PreviousStatus, item.NewStatus, item.Reason, item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { company, lifecycle });
    }

    [HttpPost("{companyId:int}/suspend")]
    public Task<IActionResult> SuspendTenant(
        int companyId, [FromBody] TenantLifecycleReasonRequest request,
        CancellationToken cancellationToken) =>
        ChangeTenantStatus(companyId, request, suspend: true, cancellationToken);

    [HttpPost("{companyId:int}/reactivate")]
    public Task<IActionResult> ReactivateTenant(
        int companyId, [FromBody] TenantLifecycleReasonRequest request,
        CancellationToken cancellationToken) =>
        ChangeTenantStatus(companyId, request, suspend: false, cancellationToken);

    private async Task<IActionResult> ChangeTenantStatus(
        int companyId, TenantLifecycleReasonRequest request, bool suspend,
        CancellationToken cancellationToken)
    {
        var actorId = GetCurrentUserId();
        if (actorId == null) return Unauthorized();

        var reason = request?.Reason ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 10 or > 1000)
            return BadRequest(new { message = "A platform-operation reason between 10 and 1,000 characters is required." });

        var result = suspend
            ? await _lifecycle.SuspendAsync(companyId, actorId.Value, reason, cancellationToken)
            : await _lifecycle.ReactivateAsync(companyId, actorId.Value, reason, cancellationToken);

        return result switch
        {
            TenantLifecycleChangeResult.Success => Ok(new
            {
                companyId,
                status = suspend ? "Suspended" : "Active",
                message = suspend ? "Tenant suspended." : "Tenant reactivated."
            }),
            TenantLifecycleChangeResult.NotFound => NotFound(),
            TenantLifecycleChangeResult.ActorNotFound => Unauthorized(),
            TenantLifecycleChangeResult.InvalidReason => BadRequest(new { message = "A reason between 10 and 1,000 characters is required." }),
            TenantLifecycleChangeResult.AlreadyInState => Conflict(new { message = suspend ? "Tenant is already suspended." : "Tenant is already active." }),
            _ => Conflict(new { message = "Tenant has an unsupported lifecycle state." })
        };
    }
}

public sealed class TenantLifecycleReasonRequest
{
    public string? Reason { get; set; }
}
