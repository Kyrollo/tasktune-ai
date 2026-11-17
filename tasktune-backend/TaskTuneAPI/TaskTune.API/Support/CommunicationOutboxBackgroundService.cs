using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;
using Task = System.Threading.Tasks.Task;

namespace TaskTune.API.Support;

public sealed class CommunicationOutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<CommunicationOutboxBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessOneAsync(stoppingToken);
                if (!processed)
                    await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Communication outbox processing failed.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskTuneDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
        var now = DateTime.UtcNow;

        // Never automatically retry an expired Processing lease: the SMTP server may
        // already have accepted the message before the worker lost its acknowledgement.
        await db.CommunicationOutbox.IgnoreQueryFilters()
            .Where(item => item.Status == "Processing" &&
                           item.LockedUntil.HasValue && item.LockedUntil.Value <= now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, "Failed")
                .SetProperty(item => item.LockedUntil, (DateTime?)null)
                .SetProperty(item => item.LockId, (Guid?)null)
                .SetProperty(item => item.LastError,
                    "Processing lease expired. Delivery outcome is unknown; verify the mail provider before any manual retry."),
                cancellationToken);

        var candidateId = await db.CommunicationOutbox.IgnoreQueryFilters()
            .Where(item => item.Status == "Queued" && item.AvailableAt <= now)
            .OrderBy(item => item.AvailableAt)
            .ThenBy(item => item.OutboxId)
            .Select(item => (long?)item.OutboxId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!candidateId.HasValue)
            return false;

        var lockId = Guid.NewGuid();
        var claimed = await db.CommunicationOutbox.IgnoreQueryFilters()
            .Where(item => item.OutboxId == candidateId.Value &&
                           item.Status == "Queued" && item.AvailableAt <= now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, "Processing")
                .SetProperty(item => item.LockedUntil, now.Add(ClaimLease))
                .SetProperty(item => item.LockId, lockId)
                .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                .SetProperty(item => item.LastAttemptAt, (DateTime?)now),
                cancellationToken);

        if (claimed != 1)
            return true;

        var outbox = await db.CommunicationOutbox.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.OutboxId == candidateId.Value &&
                item.CompanyId > 0 &&
                item.LockId == lockId &&
                item.Status == "Processing", cancellationToken);

        if (outbox == null)
            return true;

        try
        {
            var approved = await db.CommunicationApprovals.IgnoreQueryFilters()
                .AnyAsync(approval =>
                    approval.ApprovalId == outbox.ApprovalId &&
                    approval.CompanyId == outbox.CompanyId &&
                    approval.Status == "Approved", cancellationToken);
            if (!approved)
            {
                await MarkFailedAsync(db, outbox.OutboxId, lockId,
                    "Approval is no longer in the Approved state.", cancellationToken);
                return true;
            }

            var recipientIsActive = await db.Users.IgnoreQueryFilters().AnyAsync(user =>
                user.UserId == outbox.RecipientUserId &&
                user.CompanyId == outbox.CompanyId &&
                user.IsActive &&
                user.Email == outbox.RecipientEmail, cancellationToken);
            if (!recipientIsActive)
            {
                await MarkFailedAsync(db, outbox.OutboxId, lockId,
                    "The recipient is no longer an active user in the requesting company.", cancellationToken);
                return true;
            }

            var ccRecipients = DeserializeCcRecipients(outbox.CcEmailsJson);
            if (ccRecipients.Count > 25)
            {
                await MarkFailedAsync(db, outbox.OutboxId, lockId,
                    "The approved CC list exceeds the delivery limit.", cancellationToken);
                return true;
            }

            if (ccRecipients.Count > 0)
            {
                var activeCcEmails = await db.Users.IgnoreQueryFilters()
                    .Where(user => user.CompanyId == outbox.CompanyId &&
                                   user.IsActive &&
                                   user.Email != null &&
                                   ccRecipients.Contains(user.Email))
                    .Select(user => user.Email!)
                    .ToListAsync(cancellationToken);

                if (activeCcEmails.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                    ccRecipients.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    await MarkFailedAsync(db, outbox.OutboxId, lockId,
                        "One or more approved CC recipients are no longer active members of the company.", cancellationToken);
                    return true;
                }
            }

            await emailSender.SendAsync(
                outbox.RecipientEmail,
                outbox.Subject,
                outbox.Body,
                ccRecipients,
                cancellationToken);

            var sentAt = DateTime.UtcNow;
            var markedSent = await db.CommunicationOutbox.IgnoreQueryFilters()
                .Where(item => item.OutboxId == outbox.OutboxId &&
                               item.Status == "Processing" &&
                               item.LockId == lockId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, "Sent")
                    .SetProperty(item => item.SentAt, (DateTime?)sentAt)
                    .SetProperty(item => item.LockedUntil, (DateTime?)null)
                    .SetProperty(item => item.LockId, (Guid?)null)
                    .SetProperty(item => item.LastError, (string?)null),
                    cancellationToken);

            if (markedSent == 1)
            {
                await db.CommunicationLogs.IgnoreQueryFilters()
                    .Where(log => log.CommunicationId == outbox.CommunicationId &&
                                  log.CompanyId == outbox.CompanyId)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(log => log.SentAt, sentAt),
                        cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Communication outbox delivery failed for outbox item {OutboxId}.", outbox.OutboxId);
            await MarkFailedAsync(db, outbox.OutboxId, lockId,
                $"SMTP delivery failed ({exception.GetType().Name}). Check provider logs before retrying.",
                cancellationToken);
        }

        return true;
    }

    private static List<string> DeserializeCcRecipients(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return ["invalid-cc-payload"];
        }
    }

    private static Task<int> MarkFailedAsync(
        TaskTuneDbContext db,
        long outboxId,
        Guid lockId,
        string error,
        CancellationToken cancellationToken) =>
        db.CommunicationOutbox.IgnoreQueryFilters()
            .Where(item => item.OutboxId == outboxId &&
                           item.Status == "Processing" &&
                           item.LockId == lockId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, "Failed")
                .SetProperty(item => item.LockedUntil, (DateTime?)null)
                .SetProperty(item => item.LockId, (Guid?)null)
                .SetProperty(item => item.LastError, error.Length > 1000 ? error[..1000] : error),
                cancellationToken);
}
