using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum TenantSeatCapacityResult
{
    Allowed,
    NoActiveSubscription,
    LimitReached,
    TenantContextMismatch,
    TransactionRequired
}

public sealed record TenantSeatCapacityCheck(
    TenantSeatCapacityResult Result,
    int ActiveUsers,
    int PendingInvitations,
    int SeatsReserved,
    int? MaxUsers);

/// <summary>
/// Seat checks must run in the caller's serializable transaction. The tenant subscription
/// row is the per-tenant lock point, shared by invitation and direct-user provisioning.
/// </summary>
public sealed class TenantSeatCapacityService(TaskTuneDbContext db)
{
    public async Task<TenantSeatCapacityCheck> CheckAndLockAsync(
        int companyId, CancellationToken cancellationToken = default)
    {
        if (!db.CurrentTenantId.HasValue || db.CurrentTenantId.Value != companyId)
            return new(TenantSeatCapacityResult.TenantContextMismatch, 0, 0, 0, null);
        if (db.Database.IsRelational() && db.Database.CurrentTransaction == null)
            return new(TenantSeatCapacityResult.TransactionRequired, 0, 0, 0, null);

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
            return new(TenantSeatCapacityResult.NoActiveSubscription, 0, 0, 0, null);

        var activeUsers = await db.Users.CountAsync(user => user.IsActive, cancellationToken);
        var pendingInvitations = await db.AccountSecurityTokens.CountAsync(token =>
            token.Purpose == "Invitation" && token.ConsumedAt == null && token.ExpiresAt > now,
            cancellationToken);
        var reserved = activeUsers + pendingInvitations;
        var limit = subscription.Plan.MaxUsers;

        if (limit.HasValue && reserved >= limit.Value)
            return new(TenantSeatCapacityResult.LimitReached, activeUsers, pendingInvitations, reserved, limit);

        return new(TenantSeatCapacityResult.Allowed, activeUsers, pendingInvitations, reserved, limit);
    }

    public static string GetErrorMessage(TenantSeatCapacityCheck check) => check.Result switch
    {
        TenantSeatCapacityResult.NoActiveSubscription =>
            "An active or trial subscription is required before adding users.",
        TenantSeatCapacityResult.LimitReached =>
            $"The tenant has reached its plan seat limit ({check.SeatsReserved}/{check.MaxUsers} active users and pending invitations).",
        TenantSeatCapacityResult.TenantContextMismatch =>
            "The operation does not match the authenticated tenant context.",
        TenantSeatCapacityResult.TransactionRequired =>
            "Seat reservations must be performed inside a serializable transaction.",
        _ => string.Empty
    };
}
