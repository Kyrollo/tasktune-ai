using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/platform/subscriptions")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformSubscriptionsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly TenantSubscriptionAdministrationService _administration;

    public PlatformSubscriptionsController(TaskTuneDbContext db, TenantSubscriptionAdministrationService administration)
    { _db = db; _administration = administration; }

    [HttpGet("tenants/{companyId:int}")]
    public async Task<IActionResult> GetTenantSubscription(int companyId, CancellationToken cancellationToken)
    {
        if (!await _db.Companies.IgnoreQueryFilters().AnyAsync(company => company.CompanyId == companyId, cancellationToken))
            return NotFound();
        var subscription = await _db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .Select(item => new
            {
                item.SubscriptionId, item.CompanyId, item.PlanId, PlanCode = item.Plan.Code,
                PlanName = item.Plan.Name, item.Status, item.StartsAt, item.EndsAt,
                item.CreatedAt, item.UpdatedAt
            }).SingleOrDefaultAsync(cancellationToken);
        var events = await _db.TenantSubscriptionEvents.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.CompanyId == companyId).OrderByDescending(item => item.CreatedAt).Take(100)
            .Select(item => new
            {
                item.EventId, item.ActorUserId, item.Action, item.PreviousPlanId,
                item.NewPlanId, item.PreviousStatus, item.NewStatus, item.Reason, item.CreatedAt
            }).ToListAsync(cancellationToken);
        return Ok(new { subscription, events });
    }

    [HttpPut("tenants/{companyId:int}")]
    public async Task<IActionResult> AssignTenantSubscription(
        int companyId, [FromBody] AssignTenantSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetCurrentUserId();
        if (actorId == null) return Unauthorized();
        var result = await _administration.AssignSubscriptionAsync(
            companyId, actorId.Value, request.PlanId, request.Status,
            request.StartsAt, request.EndsAt, request.Reason, cancellationToken);
        return result switch
        {
            PlanAdministrationResult.Success => Ok(new { companyId, request.PlanId, request.Status, message = "Tenant subscription updated." }),
            PlanAdministrationResult.NotFound => NotFound(),
            PlanAdministrationResult.ActorNotFound => Unauthorized(),
            PlanAdministrationResult.InactivePlan => Conflict(new { message = "Inactive plans cannot be assigned." }),
            PlanAdministrationResult.InvalidStatus => BadRequest(new { message = "Status must be Trial, Active, PastDue or Cancelled." }),
            PlanAdministrationResult.InvalidPeriod => BadRequest(new { message = "Subscription end must be later than the start." }),
            PlanAdministrationResult.InvalidReason => BadRequest(new { message = "A 10–1,000 character assignment reason is required." }),
            PlanAdministrationResult.ConcurrencyConflict => Conflict(new { message = "The subscription changed concurrently. Refresh and retry." }),
            _ => BadRequest(new { message = "Subscription assignment fields are invalid." })
        };
    }
}

public sealed record AssignTenantSubscriptionRequest(int PlanId, string Status, DateTime StartsAt, DateTime? EndsAt, string Reason);
