using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public sealed record TenantEntitlementSummary(
    int CompanyId,
    string? PlanCode,
    string? PlanName,
    string? SubscriptionStatus,
    DateTime? StartsAt,
    DateTime? EndsAt,
    bool HasActiveSubscription,
    int? MaxUsers,
    long? MaxStorageBytes,
    long? MonthlyAiTokenLimit,
    IReadOnlyList<TenantFeatureEntitlement> Features);

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
    public async Task<TenantEntitlementSummary> GetTenantSummaryAsync(
        int companyId, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
            return new TenantEntitlementSummary(companyId, null, null, null, null, null, false,
                null, null, null, Array.Empty<TenantFeatureEntitlement>());

        var now = DateTime.UtcNow;
        var subscription = await db.TenantSubscriptions.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .Select(item => new
            {
                item.PlanId,
                item.Status,
                item.StartsAt,
                item.EndsAt,
                PlanCode = item.Plan.Code,
                PlanName = item.Plan.Name,
                PlanIsActive = item.Plan.IsActive,
                item.Plan.MaxUsers,
                item.Plan.MaxStorageBytes,
                item.Plan.MonthlyAiTokenLimit
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (subscription == null)
            return new TenantEntitlementSummary(companyId, null, null, null, null, null, false,
                null, null, null, Array.Empty<TenantFeatureEntitlement>());

        var active = subscription.PlanIsActive &&
            (subscription.Status == "Active" || subscription.Status == "Trial") &&
            subscription.StartsAt <= now &&
            (!subscription.EndsAt.HasValue || subscription.EndsAt.Value > now);

        var rows = await db.SubscriptionPlanFeatures.AsNoTracking()
            .Where(feature => feature.PlanId == subscription.PlanId)
            .OrderBy(feature => feature.FeatureCode)
            .Select(feature => new { feature.FeatureCode, feature.IsEnabled, feature.LimitValue })
            .ToListAsync(cancellationToken);

        var features = rows.Select(feature =>
        {
            var enabled = active && feature.IsEnabled;
            return new TenantFeatureEntitlement(
                enabled,
                feature.FeatureCode,
                subscription.PlanCode,
                enabled ? feature.LimitValue : null,
                enabled ? "Feature is enabled by the current plan." :
                    "Feature is not available on the current subscription.");
        }).ToArray();

        return new TenantEntitlementSummary(
            companyId,
            subscription.PlanCode,
            subscription.PlanName,
            subscription.Status,
            subscription.StartsAt,
            subscription.EndsAt,
            active,
            active ? subscription.MaxUsers : null,
            active ? subscription.MaxStorageBytes : null,
            active ? subscription.MonthlyAiTokenLimit : null,
            features);
    }

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
