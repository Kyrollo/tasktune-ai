using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/platform/plans")]
[Authorize(Policy = "PlatformAdmin")]
public sealed class PlatformPlansController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly TenantSubscriptionAdministrationService _administration;

    public PlatformPlansController(TaskTuneDbContext db, TenantSubscriptionAdministrationService administration)
    { _db = db; _administration = administration; }

    [HttpGet]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await _db.SubscriptionPlans.AsNoTracking().OrderBy(plan => plan.Code)
            .Select(plan => new
            {
                plan.PlanId, plan.Code, plan.Name, plan.Description, plan.IsActive,
                plan.MaxUsers, plan.MaxStorageBytes, plan.MonthlyAiTokenLimit,
                plan.CreatedAt, plan.UpdatedAt
            }).ToListAsync(cancellationToken);
        var features = await _db.SubscriptionPlanFeatures.AsNoTracking().ToListAsync(cancellationToken);
        return Ok(new { items = plans.Select(plan => new
        {
            plan.PlanId, plan.Code, plan.Name, plan.Description, plan.IsActive,
            plan.MaxUsers, plan.MaxStorageBytes, plan.MonthlyAiTokenLimit,
            plan.CreatedAt, plan.UpdatedAt,
            features = features.Where(feature => feature.PlanId == plan.PlanId)
                .Select(feature => new { feature.FeatureCode, feature.IsEnabled, feature.LimitValue })
        }) });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlan([FromBody] CreateSubscriptionPlanRequest request, CancellationToken cancellationToken)
    {
        var actorId = GetCurrentUserId();
        if (actorId == null) return Unauthorized();
        var result = await _administration.CreatePlanAsync(
            request.Code, request.Name, request.Description, request.MaxUsers,
            request.MaxStorageBytes, request.MonthlyAiTokenLimit, cancellationToken);
        if (result.Result != PlanAdministrationResult.Success)
            return MapError(result.Result);
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId,
            action: "CREATE", resourceType: "SubscriptionPlan",
            resourceId: result.Plan!.PlanId, after: result.Plan);
        return CreatedAtAction(nameof(GetPlans), new { },
            new { result.Plan.PlanId, result.Plan.Code, result.Plan.Name });
    }

    [HttpPut("{planId:int}/features/{featureCode}")]
    public async Task<IActionResult> SetFeature(
        int planId, string featureCode, [FromBody] SetPlanFeatureRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetCurrentUserId();
        if (actorId == null) return Unauthorized();
        var before = await _db.SubscriptionPlanFeatures.AsNoTracking()
            .SingleOrDefaultAsync(feature => feature.PlanId == planId && feature.FeatureCode == featureCode, cancellationToken);
        var result = await _administration.SetFeatureAsync(planId, featureCode, request.IsEnabled, request.LimitValue, cancellationToken);
        if (result.Result != PlanAdministrationResult.Success)
            return MapError(result.Result);
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId,
            action: "UPDATE", resourceType: "SubscriptionPlanFeature", resourceId: planId,
            before: before, after: result.Feature);
        return Ok(new { planId, result.Feature!.FeatureCode, result.Feature.IsEnabled, result.Feature.LimitValue });
    }

    private IActionResult MapError(PlanAdministrationResult result) => result switch
    {
        PlanAdministrationResult.InvalidInput => BadRequest(new { message = "The plan or feature fields are invalid." }),
        PlanAdministrationResult.AlreadyExists => Conflict(new { message = "A plan with this code already exists." }),
        PlanAdministrationResult.NotFound => NotFound(),
        _ => Conflict(new { message = "The plan operation could not be completed." })
    };
}

public sealed record CreateSubscriptionPlanRequest(string Code, string Name, string? Description,
    int? MaxUsers, long? MaxStorageBytes, long? MonthlyAiTokenLimit);
public sealed record SetPlanFeatureRequest(bool IsEnabled, int? LimitValue);
