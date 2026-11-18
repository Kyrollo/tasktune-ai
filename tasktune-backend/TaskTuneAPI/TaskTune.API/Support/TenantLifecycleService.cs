using System.Data;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum TenantLifecycleChangeResult { Success, NotFound, ActorNotFound, InvalidReason, InvalidState, AlreadyInState }

public sealed class TenantLifecycleService(TaskTuneDbContext db)
{
    public System.Threading.Tasks.Task<TenantLifecycleChangeResult> SuspendAsync(int companyId, int actorUserId, string reason, CancellationToken cancellationToken = default) =>
        SetStatusAsync(companyId, actorUserId, "Suspended", reason, cancellationToken);

    public System.Threading.Tasks.Task<TenantLifecycleChangeResult> ReactivateAsync(int companyId, int actorUserId, string reason, CancellationToken cancellationToken = default) =>
        SetStatusAsync(companyId, actorUserId, "Active", reason, cancellationToken);

    private async System.Threading.Tasks.Task<TenantLifecycleChangeResult> SetStatusAsync(
        int companyId, int actorUserId, string targetStatus, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 10 or > 1000)
            return TenantLifecycleChangeResult.InvalidReason;
        if (!await db.Users.IgnoreQueryFilters().AnyAsync(user => user.UserId == actorUserId && user.IsActive, cancellationToken))
            return TenantLifecycleChangeResult.ActorNotFound;

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var company = await db.Companies.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (company == null) return TenantLifecycleChangeResult.NotFound;
        if (company.Status is not ("Active" or "Suspended")) return TenantLifecycleChangeResult.InvalidState;
        if (company.Status == targetStatus) return TenantLifecycleChangeResult.AlreadyInState;

        var previousStatus = company.Status;
        var now = DateTime.UtcNow;
        company.Status = targetStatus;
        company.SuspendedAt = targetStatus == "Suspended" ? now : null;
        company.UpdatedAt = now;
        db.TenantLifecycleEvents.Add(new TenantLifecycleEvent
        {
            CompanyId = company.CompanyId, Company = company, ActorUserId = actorUserId,
            Action = targetStatus == "Suspended" ? "Suspended" : "Reactivated",
            PreviousStatus = previousStatus, NewStatus = targetStatus,
            Reason = reason.Trim(), CreatedAt = now
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return TenantLifecycleChangeResult.Success;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
