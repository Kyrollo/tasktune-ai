using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum TenantStorageCapacityResult
{
    Allowed,
    NoActiveSubscription,
    LimitExceeded,
    InvalidRequest,
    TenantContextMismatch,
    TransactionRequired
}

public sealed record TenantStorageCapacityCheck(
    TenantStorageCapacityResult Result,
    long CurrentStorageBytes,
    long RequestedBytes,
    long? MaxStorageBytes);

/// <summary>
/// Serializes storage reservations on the same per-tenant subscription row used by seat
/// reservations. Callers must keep this check and the attachment metadata insert in one
/// SERIALIZABLE transaction.
/// </summary>
public sealed class TenantStorageCapacityService(TaskTuneDbContext db)
{
    public async Task<TenantStorageCapacityCheck> CheckAndLockAsync(
        int companyId, long requestedBytes, CancellationToken cancellationToken = default)
    {
        if (requestedBytes <= 0)
            return new(TenantStorageCapacityResult.InvalidRequest, 0, requestedBytes, null);
        if (!db.CurrentTenantId.HasValue || db.CurrentTenantId.Value != companyId)
            return new(TenantStorageCapacityResult.TenantContextMismatch, 0, requestedBytes, null);
        if (db.Database.IsRelational() && db.Database.CurrentTransaction == null)
            return new(TenantStorageCapacityResult.TransactionRequired, 0, requestedBytes, null);

        TenantSubscription? subscription;
        if (db.Database.IsRelational())
        {
            subscription = await db.TenantSubscriptions
                .FromSqlInterpolated(
                    $"SELECT * FROM dbo.TenantSubscriptions WITH (UPDLOCK, HOLDLOCK) WHERE CompanyId = {companyId}")
                .Include(item => item.Plan)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            subscription = await db.TenantSubscriptions
                .Include(item => item.Plan)
                .SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        }

        var now = DateTime.UtcNow;
        if (subscription == null ||
            (subscription.Status != "Active" && subscription.Status != "Trial") ||
            subscription.StartsAt > now ||
            (subscription.EndsAt.HasValue && subscription.EndsAt.Value <= now) ||
            !subscription.Plan.IsActive)
            return new(TenantStorageCapacityResult.NoActiveSubscription, 0, requestedBytes, null);

        var currentBytes = await db.TaskAttachments
            .Select(item => (long?)item.FileSize)
            .SumAsync(cancellationToken) ?? 0L;
        var limit = subscription.Plan.MaxStorageBytes;
        if (limit.HasValue && (requestedBytes > limit.Value || currentBytes > limit.Value - requestedBytes))
            return new(TenantStorageCapacityResult.LimitExceeded, currentBytes, requestedBytes, limit);

        return new(TenantStorageCapacityResult.Allowed, currentBytes, requestedBytes, limit);
    }

    public static string GetErrorMessage(TenantStorageCapacityCheck check) => check.Result switch
    {
        TenantStorageCapacityResult.NoActiveSubscription =>
            "An active or trial subscription is required before uploading files.",
        TenantStorageCapacityResult.LimitExceeded =>
            $"The tenant storage limit would be exceeded ({check.CurrentStorageBytes + check.RequestedBytes}/{check.MaxStorageBytes} bytes).",
        TenantStorageCapacityResult.TenantContextMismatch =>
            "The operation does not match the authenticated tenant context.",
        TenantStorageCapacityResult.TransactionRequired =>
            "Storage reservations must be performed inside a serializable transaction.",
        TenantStorageCapacityResult.InvalidRequest =>
            "The requested storage amount is invalid.",
        _ => string.Empty
    };
}
