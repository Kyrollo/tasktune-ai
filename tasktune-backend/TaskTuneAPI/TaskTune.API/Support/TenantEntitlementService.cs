using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public sealed record TenantFeatureEntitlement(
    bool Enabled,
    string FeatureCode,
    string? PlanCode,
    int? LimitValue,
    string Reason);

/// <summary>
/// Evaluates subscription-backed feature access. Missing, expired, cancelled, past-due,
/// inactive-plan or missing feature rows all fail closed.
/// </summary>
public sealed class TenantEntitlementService(TaskTuneDbContext db)
{
    public async Task<TenantFeatureEntitlement> GetFeatureAccessAsync(
        int companyId, string featureCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeFeatureCode(featureCode);
        if (companyId <= 0 || normalizedCode == null)
            return new(false, normalizedCode ?? string.Empty, null, null, "Invalid tenant or feature code.");

        var now = DateTime.UtcNow;
        var subscription = await db.TenantSubscriptions
            .Where(item => item.CompanyId == companyId
                && (item.Status == "Active" || item.Status == "Trial")
                && item.StartsAt <= now
                && (!item.EndsAt.HasValue || item.EndsAt > now)
                && item.Plan.IsActive)
            .Select(item => new { item.PlanId, PlanCode = item.Plan.Code })
            .SingleOrDefaultAsync(cancellationToken);

        if (subscription == null)
            return new(false, normalizedCode, null, null, "No active subscription is assigned to this tenant.");

        var feature = await db.SubscriptionPlanFeatures
            .Where(item => item.PlanId == subscription.PlanId && item.FeatureCode == normalizedCode)
            .Select(item => new { item.IsEnabled, item.LimitValue })
            .SingleOrDefaultAsync(cancellationToken);

        if (feature == null)
            return new(false, normalizedCode, subscription.PlanCode, null, "The assigned plan does not include this feature.");

        return feature.IsEnabled
            ? new(true, normalizedCode, subscription.PlanCode, feature.LimitValue, "Feature is enabled by the current plan.")
            : new(false, normalizedCode, subscription.PlanCode, feature.LimitValue, "The assigned plan disables this feature.");
    }

    private static string? NormalizeFeatureCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var code = value.Trim().ToLowerInvariant();
        if (code.Length is < 3 or > 100) return null;
        return code.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')
            ? code
            : null;
    }
}
