using System.Text.Json;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

/// <summary>
/// SQL Server-backed, leased outbox dispatcher. Disabled unless Email:Outbox:Enabled is true.
/// SMTP is at-least-once: a process/network failure after remote acceptance but before the
/// Sent state commits can cause a retry. A stable Message-ID helps downstream deduplication
/// but does not provide an exactly-once guarantee for SMTP.
/// </summary>
public sealed class CommunicationOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CommunicationOutboxWorker> logger) : BackgroundService
{
    private const int DefaultLeaseSeconds = 180;
    private const int DefaultMaxAttempts = 5;

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!configuration.GetValue<bool>("Email:Outbox:Enabled"))
            {
                await DelayAsync(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TaskTuneDbContext>();
                var item = await TryClaimNextAsync(
                    db,
                    configuration.GetValue<int?>("Email:Outbox:LeaseSeconds") ?? DefaultLeaseSeconds,
                    stoppingToken);

                if (item == null)
                {
                    await DelayAsync(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                var sender = scope.ServiceProvider.GetRequiredService<ITransactionalEmailSender>();
                await DeliverAsync(db, sender, item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Communication outbox loop failed. Exception type: {ExceptionType}",
                    exception.GetType().Name);
                await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Atomically claims one due row with UPDLOCK/READPAST. Expired Processing leases
    /// are reclaimed; Attempts increments in the same statement that assigns the lease.
    /// </summary>
    public static async System.Threading.Tasks.Task<CommunicationOutbox?> TryClaimNextAsync(
        TaskTuneDbContext db,
        int leaseSeconds = DefaultLeaseSeconds,
        CancellationToken cancellationToken = default)
    {
        leaseSeconds = Math.Clamp(leaseSeconds, 30, 900);
        var leaseToken = Guid.NewGuid();

        var claimed = await db.CommunicationOutbox
            .FromSqlInterpolated($"""
                ;WITH next_row AS
                (
                    SELECT TOP (1) *
                    FROM dbo.CommunicationOutbox WITH (UPDLOCK, READPAST, ROWLOCK)
                    WHERE
                        (Status = N'Pending' AND NextAttemptAt <= SYSUTCDATETIME())
                        OR
                        (Status = N'Processing' AND LeaseExpiresAt <= SYSUTCDATETIME())
                    ORDER BY
                        CASE WHEN Status = N'Pending' THEN NextAttemptAt ELSE LeaseExpiresAt END,
                        OutboxId
                )
                UPDATE next_row
                SET
                    Status = N'Processing',
                    Attempts = Attempts + 1,
                    LeaseToken = {leaseToken},
                    LeaseExpiresAt = DATEADD(SECOND, {leaseSeconds}, SYSUTCDATETIME())
                OUTPUT INSERTED.*;
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return claimed.SingleOrDefault();
    }

    private async System.Threading.Tasks.Task DeliverAsync(
        TaskTuneDbContext db,
        ITransactionalEmailSender sender,
        CommunicationOutbox item,
        CancellationToken cancellationToken)
    {
        try
        {
            var cc = JsonSerializer.Deserialize<List<string>>(item.CcEmailsJson) ?? [];
            await sender.SendCommunicationAsync(
                item.RecipientEmail,
                cc,
                item.Subject,
                item.Body,
                item.IdempotencyKey,
                cancellationToken);

            var sentAt = DateTime.UtcNow;
            var updated = await db.CommunicationOutbox.IgnoreQueryFilters()
                .Where(x => x.OutboxId == item.OutboxId &&
                    x.Status == "Processing" && x.LeaseToken == item.LeaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, "Sent")
                    .SetProperty(x => x.SentAt, sentAt)
                    .SetProperty(x => x.LastError, (string?)null)
                    .SetProperty(x => x.LeaseToken, (Guid?)null)
                    .SetProperty(x => x.LeaseExpiresAt, (DateTime?)null),
                    cancellationToken);

            if (updated == 0)
                logger.LogWarning("Outbox delivery completed after its lease was lost. OutboxId: {OutboxId}", item.OutboxId);
            else
                logger.LogInformation("Communication outbox item sent. OutboxId: {OutboxId}", item.OutboxId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Keep the lease. Another worker will reclaim it after expiry. SMTP acceptance may be unknown.
            throw;
        }
        catch (Exception exception)
        {
            var maxAttempts = Math.Clamp(
                configuration.GetValue<int?>("Email:Outbox:MaxAttempts") ?? DefaultMaxAttempts, 1, 20);
            var deadLetter = item.Attempts >= maxAttempts;
            var retryAt = DateTime.UtcNow.Add(GetRetryDelay(item.Attempts));
            var safeError = exception is SmtpException
                ? "SMTP delivery failed; remote acceptance may be uncertain. Review provider logs before manual replay."
                : "Delivery adapter failed. Review worker diagnostics before manual replay.";

            var updated = await db.CommunicationOutbox.IgnoreQueryFilters()
                .Where(x => x.OutboxId == item.OutboxId &&
                    x.Status == "Processing" && x.LeaseToken == item.LeaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, deadLetter ? "DeadLetter" : "Pending")
                    .SetProperty(x => x.NextAttemptAt, retryAt)
                    .SetProperty(x => x.LastError, safeError)
                    .SetProperty(x => x.LeaseToken, (Guid?)null)
                    .SetProperty(x => x.LeaseExpiresAt, (DateTime?)null),
                    cancellationToken);

            if (updated == 0)
                logger.LogWarning("Outbox failure recorded after its lease was lost. OutboxId: {OutboxId}", item.OutboxId);
            else if (deadLetter)
                logger.LogError("Communication outbox item moved to DeadLetter. OutboxId: {OutboxId}", item.OutboxId);
            else
                logger.LogWarning(
                    "Communication outbox item scheduled for retry. OutboxId: {OutboxId}; Attempt: {Attempt}",
                    item.OutboxId, item.Attempts);
        }
    }

    private static TimeSpan GetRetryDelay(int attempts)
    {
        var exponent = Math.Clamp(attempts - 1, 0, 8);
        var seconds = Math.Min(3600, 15 * Math.Pow(2, exponent)) + Random.Shared.Next(0, 16);
        return TimeSpan.FromSeconds(seconds);
    }

    private static System.Threading.Tasks.Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        System.Threading.Tasks.Task.Delay(delay, cancellationToken);
}
