namespace TaskTune.API.Support;

public sealed record CommunicationOutboxQueueMetrics(
    int Pending, int Processing, int Sent, int Failed, int DeadLetter,
    DateTime? OldestPendingAtUtc, double? OldestPendingAgeSeconds,
    DateTime? OldestProcessingAtUtc, double? OldestProcessingAgeSeconds,
    int QueueAgeWarningMinutes, bool QueueAgeWarning, bool DeliveryWorkerEnabled);

/// <summary>Returns tenant-scoped, content-free delivery backlog metrics.</summary>
public sealed class CommunicationOutboxMetricsService(TaskTune.Infrastructure.Models.TaskTuneDbContext db)
{
    public async System.Threading.Tasks.Task<CommunicationOutboxQueueMetrics> GetAsync(
        int companyId, int queueAgeWarningMinutes, bool deliveryWorkerEnabled,
        CancellationToken cancellationToken = default)
    {
        queueAgeWarningMinutes = Math.Clamp(queueAgeWarningMinutes, 1, 1440);
        var query = db.CommunicationOutbox.AsNoTracking()
            .Where(item => item.CompanyId == companyId);
        var statusCounts = await query.GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var counts = statusCounts.ToDictionary(x => x.Status, x => x.Count, StringComparer.Ordinal);

        var oldestPending = await query.Where(item => item.Status == "Pending")
            .Select(item => (DateTime?)item.CreatedAt).MinAsync(cancellationToken);
        var oldestProcessing = await query.Where(item => item.Status == "Processing")
            .Select(item => (DateTime?)item.CreatedAt).MinAsync(cancellationToken);

        var now = DateTime.UtcNow;
        double? pendingAge = oldestPending.HasValue ? Math.Max(0, (now - oldestPending.Value).TotalSeconds) : null;
        double? processingAge = oldestProcessing.HasValue ? Math.Max(0, (now - oldestProcessing.Value).TotalSeconds) : null;
        var queueAgeWarning = deliveryWorkerEnabled && pendingAge.HasValue &&
            pendingAge.Value >= TimeSpan.FromMinutes(queueAgeWarningMinutes).TotalSeconds;

        return new CommunicationOutboxQueueMetrics(
            counts.GetValueOrDefault("Pending"), counts.GetValueOrDefault("Processing"),
            counts.GetValueOrDefault("Sent"), counts.GetValueOrDefault("Failed"),
            counts.GetValueOrDefault("DeadLetter"), oldestPending, pendingAge,
            oldestProcessing, processingAge, queueAgeWarningMinutes, queueAgeWarning, deliveryWorkerEnabled);
    }
}
