using System.Data;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum PlanAdministrationResult
{
    Success, InvalidInput, NotFound, AlreadyExists, InactivePlan,
    ActorNotFound, InvalidStatus, InvalidPeriod, InvalidReason, ConcurrencyConflict
}

public sealed class TenantSubscriptionAdministrationService(TaskTuneDbContext db)
{
    public async Task<(PlanAdministrationResult Result, SubscriptionPlan? Plan)> CreatePlanAsync(
        string code, string name, string? description, int? maxUsers,
        long? maxStorageBytes, long? monthlyAiTokenLimit, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(code, 64);
        if (normalizedCode == null || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 ||
            (description?.Length > 500) || (maxUsers.HasValue && maxUsers.Value <= 0) ||
            (maxStorageBytes.HasValue && maxStorageBytes.Value <= 0) ||
            (monthlyAiTokenLimit.HasValue && monthlyAiTokenLimit.Value < 0))
            return (PlanAdministrationResult.InvalidInput, null);
        if (await db.SubscriptionPlans.AnyAsync(plan => plan.Code == normalizedCode, cancellationToken))
            return (PlanAdministrationResult.AlreadyExists, null);

        var now = DateTime.UtcNow;
        var plan = new SubscriptionPlan
        {
            Code = normalizedCode, Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            MaxUsers = maxUsers, MaxStorageBytes = maxStorageBytes,
            MonthlyAiTokenLimit = monthlyAiTokenLimit, IsActive = true,
            CreatedAt = now, UpdatedAt = now
        };
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return (PlanAdministrationResult.Success, plan);
    }

    public async Task<(PlanAdministrationResult Result, SubscriptionPlanFeature? Feature)> SetFeatureAsync(
        int planId, string featureCode, bool enabled, int? limitValue,
        CancellationToken cancellationToken = default)
    {
        var normalizedFeature = NormalizeCode(featureCode, 100);
        if (planId <= 0 || normalizedFeature == null || (limitValue.HasValue && limitValue.Value < 0))
            return (PlanAdministrationResult.InvalidInput, null);
        if (!await db.SubscriptionPlans.AnyAsync(plan => plan.PlanId == planId, cancellationToken))
            return (PlanAdministrationResult.NotFound, null);

        var feature = await db.SubscriptionPlanFeatures.SingleOrDefaultAsync(
            item => item.PlanId == planId && item.FeatureCode == normalizedFeature, cancellationToken);
        if (feature == null)
        {
            feature = new SubscriptionPlanFeature { PlanId = planId, FeatureCode = normalizedFeature, IsEnabled = enabled, LimitValue = limitValue };
            db.SubscriptionPlanFeatures.Add(feature);
        }
        else
        {
            feature.IsEnabled = enabled;
            feature.LimitValue = limitValue;
        }
        await db.SaveChangesAsync(cancellationToken);
        return (PlanAdministrationResult.Success, feature);
    }

    public async Task<PlanAdministrationResult> AssignSubscriptionAsync(
        int companyId, int actorUserId, int planId, string status,
        DateTime startsAt, DateTime? endsAt, string reason,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = status?.Trim();
        if (companyId <= 0 || actorUserId <= 0 || planId <= 0)
            return PlanAdministrationResult.InvalidInput;
        if (normalizedStatus is not ("Trial" or "Active" or "PastDue" or "Cancelled"))
            return PlanAdministrationResult.InvalidStatus;
        if (endsAt.HasValue && endsAt.Value <= startsAt)
            return PlanAdministrationResult.InvalidPeriod;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 10 or > 1000)
            return PlanAdministrationResult.InvalidReason;
        if (!await db.Users.IgnoreQueryFilters().AnyAsync(user => user.UserId == actorUserId && user.IsActive, cancellationToken))
            return PlanAdministrationResult.ActorNotFound;

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        if (!await db.Companies.IgnoreQueryFilters().AnyAsync(company => company.CompanyId == companyId, cancellationToken))
            return PlanAdministrationResult.NotFound;
        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(item => item.PlanId == planId, cancellationToken);
        if (plan == null) return PlanAdministrationResult.NotFound;
        if (!plan.IsActive) return PlanAdministrationResult.InactivePlan;

        var subscription = await db.TenantSubscriptions.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        var previousPlanId = subscription?.PlanId;
        var previousStatus = subscription?.Status ?? string.Empty;
        var action = subscription == null ? "Assigned" :
            previousPlanId != planId ? "PlanChanged" : "StatusChanged";
        var now = DateTime.UtcNow;
        if (subscription == null)
        {
            subscription = new TenantSubscription
            {
                CompanyId = companyId, PlanId = planId, Status = normalizedStatus!,
                StartsAt = startsAt, EndsAt = endsAt, CreatedAt = now, UpdatedAt = now
            };
            db.TenantSubscriptions.Add(subscription);
        }
        else
        {
            subscription.PlanId = planId;
            subscription.Status = normalizedStatus!;
            subscription.StartsAt = startsAt;
            subscription.EndsAt = endsAt;
            subscription.UpdatedAt = now;
        }
        db.TenantSubscriptionEvents.Add(new TenantSubscriptionEvent
        {
            CompanyId = companyId, ActorUserId = actorUserId,
            PreviousPlanId = previousPlanId, NewPlanId = planId,
            Action = action, PreviousStatus = previousStatus, NewStatus = normalizedStatus!,
            Reason = reason.Trim(), CreatedAt = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return PlanAdministrationResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            return PlanAdministrationResult.ConcurrencyConflict;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? NormalizeCode(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var code = value.Trim().ToLowerInvariant();
        if (code.Length < 2 || code.Length > maximumLength) return null;
        return code.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')
            ? code : null;
    }
}
