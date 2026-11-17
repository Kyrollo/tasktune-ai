using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/platform")]
[AllowAnonymous]
public sealed class PlatformTenantsController : ControllerBase
{
    private readonly TaskTuneDbContext _db;
    private readonly IConfiguration _configuration;

    public PlatformTenantsController(TaskTuneDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        if (!TryAuthorizePlatformOperator())
            return NotFound();

        var plans = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SubscriptionPlanId)
            .Select(p => new
            {
                p.SubscriptionPlanId,
                p.Code,
                p.Name,
                p.Description,
                p.MaxActiveEmployees,
                p.MaxStorageBytes,
                p.MaxMonthlyAiRequests,
                p.EnabledModulesJson,
                p.BillingMode,
                p.PriceMinorUnits,
                p.CurrencyCode,
                p.IsPublic
            })
            .ToListAsync();

        return Ok(new { items = plans });
    }

    [HttpPut("tenants/{companyId:int}/subscription")]
    public async Task<IActionResult> AssignSubscription(int companyId, [FromBody] AssignTenantSubscriptionRequest request)
    {
        if (!TryAuthorizePlatformOperator())
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.PlanCode) ||
            request.PlanCode.Length > 50 ||
            request.Status is not ("trialing" or "active" or "past_due" or "cancelled" or "suspended"))
            return BadRequest(new { message = "Plan code or subscription status is invalid." });

        var companyExists = await _db.Companies.AnyAsync(c => c.CompanyId == companyId);
        if (!companyExists) return NotFound(new { message = "Company not found." });

        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p =>
            p.Code == request.PlanCode.Trim().ToLowerInvariant() &&
            p.IsActive);
        if (plan == null) return BadRequest(new { message = "The specified active plan does not exist." });

        var periodStart = request.CurrentPeriodStart ?? DateTime.UtcNow;
        var periodEnd = request.CurrentPeriodEnd ?? periodStart.AddMonths(1);
        if (periodEnd <= periodStart)
            return BadRequest(new { message = "Subscription period end must be later than period start." });
        if (request.Status == "trialing" &&
            (!request.TrialEndsAt.HasValue || request.TrialEndsAt.Value <= DateTime.UtcNow))
            return BadRequest(new { message = "A trial subscription requires a future trial end date." });

        var subscription = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);
        var created = subscription == null;
        if (subscription == null)
        {
            subscription = new CompanySubscription
            {
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow
            };
            _db.CompanySubscriptions.Add(subscription);
        }

        subscription.SubscriptionPlanId = plan.SubscriptionPlanId;
        subscription.Status = request.Status;
        subscription.TrialEndsAt = request.TrialEndsAt;
        subscription.CurrentPeriodStart = periodStart;
        subscription.CurrentPeriodEnd = periodEnd;
        subscription.CancelAtPeriodEnd = request.CancelAtPeriodEnd;
        subscription.CancelledAt = request.Status == "cancelled" ? DateTime.UtcNow : null;
        subscription.ExternalCustomerId = request.ExternalCustomerId?.Trim();
        subscription.ExternalSubscriptionId = request.ExternalSubscriptionId?.Trim();
        subscription.BillingProvider = request.BillingProvider?.Trim();
        subscription.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The subscription was changed concurrently. Refresh and retry." });
        }

        Log.Information(
            "Platform operator {OperatorIp} {SubscriptionAction} subscription for company {CompanyId} to plan {PlanCode} with status {Status}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            created ? "created" : "updated",
            companyId,
            plan.Code,
            subscription.Status);

        return Ok(new
        {
            subscription.CompanySubscriptionId,
            subscription.CompanyId,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            subscription.Status,
            subscription.TrialEndsAt,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            subscription.ExternalCustomerId,
            subscription.ExternalSubscriptionId,
            subscription.BillingProvider
        });
    }

    private bool TryAuthorizePlatformOperator()
    {
        var expected = _configuration["PLATFORM_ADMIN_API_KEY"];
        var supplied = Request.Headers["X-Platform-Admin-Key"].ToString();

        if (string.IsNullOrWhiteSpace(expected) ||
            Encoding.UTF8.GetByteCount(expected) < 32 ||
            string.IsNullOrWhiteSpace(supplied))
            return false;

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}

public sealed record AssignTenantSubscriptionRequest(
    string PlanCode,
    string Status,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? TrialEndsAt,
    bool CancelAtPeriodEnd,
    string? ExternalCustomerId,
    string? ExternalSubscriptionId,
    string? BillingProvider);
