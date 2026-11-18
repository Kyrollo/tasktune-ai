using System.Data;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum UsageRecordResult
{
    Recorded,
    Duplicate,
    IdempotencyConflict,
    InvalidInput,
    MissingTenantContext
}

public sealed record TenantUsageSummary(
    int CompanyId,
    string? PlanCode,
    bool HasActiveSubscription,
    int Year,
    int Month,
    int ActiveUsers,
    int PendingInvitations,
    int SeatsReserved,
    int? MaxUsers,
    long TaskAttachmentStorageBytes,
    long? MaxStorageBytes,
    int AiActions,
    long? AiTokensUsed,
    bool AiTokenUsageAvailable,
    long? MonthlyAiTokenLimit,
    bool SeatLimitExceeded,
    bool StorageLimitExceeded,
    bool? AiTokenLimitExceeded);

/// <summary>
/// Measures real tenant activity from the source tables and records provider-reported AI token
/// usage idempotently. It does not estimate tokens or claim totals when no provider usage exists.
/// </summary>
public sealed class TenantUsageMeterService(TaskTuneDbContext db)
{
    public async Task<TenantUsageSummary> GetCurrentTenantSummaryAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        var companyId = db.CurrentTenantId;
        if (!companyId.HasValue || companyId.Value <= 0)
            throw new InvalidOperationException("A tenant context is required for usage metering.");
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "A valid UTC calendar month is required.");

        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var now = DateTime.UtcNow;

        var plan = await db.TenantSubscriptions.AsNoTracking()
            .Where(item => item.CompanyId == companyId.Value &&
                (item.Status == "Active" || item.Status == "Trial") &&
                item.StartsAt <= now && (!item.EndsAt.HasValue || item.EndsAt > now) &&
                item.Plan.IsActive)
            .Select(item => new
            {
                item.Plan.Code,
                item.Plan.MaxUsers,
                item.Plan.MaxStorageBytes,
                item.Plan.MonthlyAiTokenLimit
            })
            .SingleOrDefaultAsync(cancellationToken);

        var activeUsers = await db.Users.CountAsync(user => user.IsActive, cancellationToken);
        var pendingInvitations = await db.AccountSecurityTokens.CountAsync(token =>
            token.Purpose == "Invitation" && token.ConsumedAt == null && token.ExpiresAt > now,
            cancellationToken);
        var attachmentBytes = await db.TaskAttachments
            .Select(attachment => (long?)attachment.FileSize)
            .SumAsync(cancellationToken) ?? 0L;
        var aiActions = await db.AIActionsLogs.CountAsync(action =>
            action.Timestamp >= start && action.Timestamp < end, cancellationToken);

        var tokenEventCount = await db.TenantUsageEvents.CountAsync(item =>
            item.MeterCode == "ai.tokens" && item.OccurredAt >= start && item.OccurredAt < end,
            cancellationToken);
        long? aiTokens = null;
        if (tokenEventCount > 0)
        {
            aiTokens = await db.TenantUsageEvents
                .Where(item => item.MeterCode == "ai.tokens" && item.OccurredAt >= start && item.OccurredAt < end)
                .SumAsync(item => item.Quantity, cancellationToken);
        }

        var seatsReserved = activeUsers + pendingInvitations;
        var maxUsers = plan?.MaxUsers;
        var maxStorage = plan?.MaxStorageBytes;
        var tokenLimit = plan?.MonthlyAiTokenLimit;
        return new TenantUsageSummary(
            companyId.Value,
            plan?.Code,
            plan != null,
            year,
            month,
            activeUsers,
            pendingInvitations,
            seatsReserved,
            maxUsers,
            attachmentBytes,
            maxStorage,
            aiActions,
            aiTokens,
            aiTokens.HasValue,
            tokenLimit,
            maxUsers.HasValue && seatsReserved > maxUsers.Value,
            maxStorage.HasValue && attachmentBytes > maxStorage.Value,
            tokenLimit.HasValue && aiTokens.HasValue ? aiTokens.Value > tokenLimit.Value : null);
    }

    public async Task<TenantUsageSummary> GetPlatformTenantSummaryAsync(
        int companyId, int year, int month, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "A valid UTC calendar month is required.");

        var exists = await db.Companies.IgnoreQueryFilters().AnyAsync(
            company => company.CompanyId == companyId, cancellationToken);
        if (!exists)
            throw new KeyNotFoundException("Tenant not found.");

        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var now = DateTime.UtcNow;

        var plan = await db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.CompanyId == companyId &&
                (item.Status == "Active" || item.Status == "Trial") &&
                item.StartsAt <= now && (!item.EndsAt.HasValue || item.EndsAt > now) &&
                item.Plan.IsActive)
            .Select(item => new
            {
                item.Plan.Code,
                item.Plan.MaxUsers,
                item.Plan.MaxStorageBytes,
                item.Plan.MonthlyAiTokenLimit
            })
            .SingleOrDefaultAsync(cancellationToken);

        var activeUsers = await db.Users.IgnoreQueryFilters()
            .CountAsync(user => user.CompanyId == companyId && user.IsActive, cancellationToken);
        var pendingInvitations = await db.AccountSecurityTokens.IgnoreQueryFilters()
            .CountAsync(token => token.CompanyId == companyId &&
                token.Purpose == "Invitation" && token.ConsumedAt == null && token.ExpiresAt > now,
                cancellationToken);
        var attachmentBytes = await db.TaskAttachments.IgnoreQueryFilters()
            .Where(attachment => attachment.Task.Project.CompanyId == companyId)
            .Select(attachment => (long?)attachment.FileSize)
            .SumAsync(cancellationToken) ?? 0L;
        var aiActions = await db.AIActionsLogs.IgnoreQueryFilters()
            .CountAsync(action => action.Timestamp >= start && action.Timestamp < end &&
                ((action.UserId.HasValue && action.User.CompanyId == companyId) ||
                 (action.TaskId.HasValue && action.Task.Project.CompanyId == companyId)),
                cancellationToken);

        var tokenEvents = db.TenantUsageEvents.IgnoreQueryFilters()
            .Where(item => item.CompanyId == companyId && item.MeterCode == "ai.tokens" &&
                item.OccurredAt >= start && item.OccurredAt < end);
        var tokenEventCount = await tokenEvents.CountAsync(cancellationToken);
        long? aiTokens = tokenEventCount == 0
            ? null
            : await tokenEvents.SumAsync(item => item.Quantity, cancellationToken);

        var seatsReserved = activeUsers + pendingInvitations;
        var maxUsers = plan?.MaxUsers;
        var maxStorage = plan?.MaxStorageBytes;
        var tokenLimit = plan?.MonthlyAiTokenLimit;
        return new TenantUsageSummary(
            companyId, plan?.Code, plan != null, year, month,
            activeUsers, pendingInvitations, seatsReserved, maxUsers,
            attachmentBytes, maxStorage, aiActions, aiTokens, aiTokens.HasValue,
            tokenLimit,
            maxUsers.HasValue && seatsReserved > maxUsers.Value,
            maxStorage.HasValue && attachmentBytes > maxStorage.Value,
            tokenLimit.HasValue && aiTokens.HasValue ? aiTokens.Value > tokenLimit.Value : null);
    }

    /// <summary>
    /// Adds provider-reported token usage to the current DbContext unit of work. Callers
    /// save it with the AI action/message record so usage cannot commit independently.
    /// The caller must supply a fresh opaque idempotency key.
    /// </summary>
    public UsageRecordResult AddAiTokenUsageEvent(
        long quantity, string idempotencyKey, string source, DateTime occurredAt)
    {
        var companyId = db.CurrentTenantId;
        if (!companyId.HasValue || companyId.Value <= 0)
            return UsageRecordResult.MissingTenantContext;
        if (quantity <= 0 ||
            string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Trim().Length is < 16 or > 128 ||
            string.IsNullOrWhiteSpace(source) ||
            source.Trim().Length > 100 ||
            occurredAt == default ||
            occurredAt > DateTime.UtcNow.AddMinutes(5))
            return UsageRecordResult.InvalidInput;

        db.TenantUsageEvents.Add(new TenantUsageEvent
        {
            CompanyId = companyId.Value,
            MeterCode = "ai.tokens",
            Quantity = quantity,
            IdempotencyKey = idempotencyKey.Trim(),
            Source = source.Trim(),
            OccurredAt = occurredAt,
            CreatedAt = DateTime.UtcNow
        });
        return UsageRecordResult.Recorded;
    }

    public async Task<UsageRecordResult> RecordAiTokensAsync(
        long quantity, string idempotencyKey, string source,
        DateTime occurredAt, CancellationToken cancellationToken = default)
    {
        var companyId = db.CurrentTenantId;
        if (!companyId.HasValue || companyId.Value <= 0)
            return UsageRecordResult.MissingTenantContext;
        if (quantity <= 0 ||
            string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Trim().Length is < 16 or > 128 ||
            string.IsNullOrWhiteSpace(source) ||
            source.Trim().Length > 100 ||
            occurredAt == default ||
            occurredAt > DateTime.UtcNow.AddMinutes(5))
            return UsageRecordResult.InvalidInput;

        var key = idempotencyKey.Trim();
        var normalizedSource = source.Trim();
        var existing = await db.TenantUsageEvents.SingleOrDefaultAsync(item =>
            item.CompanyId == companyId.Value && item.IdempotencyKey == key,
            cancellationToken);
        if (existing != null)
            return existing.MeterCode == "ai.tokens" && existing.Quantity == quantity &&
                existing.Source == normalizedSource && existing.OccurredAt == occurredAt
                ? UsageRecordResult.Duplicate
                : UsageRecordResult.IdempotencyConflict;

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var usageEvent = new TenantUsageEvent
        {
            CompanyId = companyId.Value,
            MeterCode = "ai.tokens",
            Quantity = quantity,
            IdempotencyKey = key,
            Source = normalizedSource,
            OccurredAt = occurredAt,
            CreatedAt = DateTime.UtcNow
        };
        db.TenantUsageEvents.Add(usageEvent);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
            return UsageRecordResult.Recorded;
        }
        catch (DbUpdateException)
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            db.Entry(usageEvent).State = EntityState.Detached;
            var persisted = await db.TenantUsageEvents.SingleOrDefaultAsync(item =>
                item.CompanyId == companyId.Value && item.IdempotencyKey == key,
                cancellationToken);
            if (persisted == null) throw;
            return persisted.MeterCode == "ai.tokens" && persisted.Quantity == quantity &&
                persisted.Source == normalizedSource && persisted.OccurredAt == occurredAt
                ? UsageRecordResult.Duplicate
                : UsageRecordResult.IdempotencyConflict;
        }
    }
}
