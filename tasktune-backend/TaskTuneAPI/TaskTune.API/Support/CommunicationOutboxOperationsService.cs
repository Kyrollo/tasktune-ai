using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum CommunicationOutboxReplayResult
{
    Success,
    NotFound,
    NotDeadLetter,
    InvalidApprovalState,
    ProviderConfirmationRequired,
    Conflict
}

public sealed class CommunicationOutboxOperationsService(TaskTuneDbContext db)
{
    public async Task<CommunicationOutboxReplayResult> ReplayDeadLetterAsync(
        long outboxId,
        int companyId,
        int actorUserId,
        string reason,
        bool providerConfirmedNotAccepted,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 1000)
            return CommunicationOutboxReplayResult.Conflict;
        if (!providerConfirmedNotAccepted)
            return CommunicationOutboxReplayResult.ProviderConfirmationRequired;

        var item = await db.CommunicationOutbox
            .Include(outbox => outbox.Approval)
            .FirstOrDefaultAsync(outbox =>
                outbox.OutboxId == outboxId &&
                outbox.CompanyId == companyId,
                cancellationToken);

        if (item is null)
            return CommunicationOutboxReplayResult.NotFound;

        if (!string.Equals(item.Status, "DeadLetter", StringComparison.Ordinal))
            return CommunicationOutboxReplayResult.NotDeadLetter;

        if (!string.Equals(item.Approval.Status, "Approved", StringComparison.Ordinal))
            return CommunicationOutboxReplayResult.InvalidApprovalState;

        var previousAttempts = item.Attempts;
        var now = DateTime.UtcNow;
        item.Status = "Pending";
        item.Attempts = 0;
        item.NextAttemptAt = now;
        item.LeaseToken = null;
        item.LeaseExpiresAt = null;
        item.LastError = null;

        db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = "communication.outbox.replay",
            ResourceType = "CommunicationOutbox",
            ResourceId = null,
            ChangesJson = JsonSerializer.Serialize(new
            {
                outboxId,
                approvalId = item.ApprovalId,
                previousStatus = "DeadLetter",
                nextStatus = "Pending",
                previousAttempts,
                idempotencyKeyPreserved = item.IdempotencyKey,
                providerNonAcceptanceConfirmed = providerConfirmedNotAccepted,
                reconciliationReason = reason.Trim()
            }),
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            Timestamp = now
        });

        try
        {
            // The queue row and audit record are saved atomically. RowVersion prevents
            // replay racing a worker/another operator that already changed the row.
            await db.SaveChangesAsync(cancellationToken);
            return CommunicationOutboxReplayResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return CommunicationOutboxReplayResult.Conflict;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return CommunicationOutboxReplayResult.Conflict;
        }
    }
}
