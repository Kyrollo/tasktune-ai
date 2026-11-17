using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

using TaskTuneDbContext = TaskTune.Infrastructure.Models.TaskTuneDbContext;
using CompanySubscription = TaskTune.Infrastructure.Models.CompanySubscription;
using SubscriptionPlan = TaskTune.Infrastructure.Models.SubscriptionPlan;
using TenantUsageEvent = TaskTune.Infrastructure.Models.TenantUsageEvent;

namespace TaskTune.API.Support;

public sealed class TenantEntitlementException : Exception
{
    public int StatusCode { get; }

    public TenantEntitlementException(string message, int statusCode = 403)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed class TenantEntitlementService
{
    private readonly TaskTuneDbContext _db;

    public TenantEntitlementService(TaskTuneDbContext db) => _db = db;

    public async Task<(CompanySubscription Subscription, SubscriptionPlan Plan)> GetCurrentAsync(int companyId)
    {
        var result = await (
            from subscription in _db.CompanySubscriptions.AsNoTracking()
            join plan in _db.SubscriptionPlans.AsNoTracking()
                on subscription.SubscriptionPlanId equals plan.SubscriptionPlanId
            where subscription.CompanyId == companyId
            select new { Subscription = subscription, Plan = plan })
            .FirstOrDefaultAsync();

        if (result == null)
            throw new TenantEntitlementException(
                "This company has no subscription record. Contact the platform administrator.",
                StatusCodes.Status402PaymentRequired);

        EnsureSubscriptionActive(result.Subscription, result.Plan);
        return (result.Subscription, result.Plan);
    }

    public async Task EnsureModuleEnabledAsync(int companyId, string moduleCode)
    {
        var (_, plan) = await GetCurrentAsync(companyId);
        var modules = ParseModules(plan.EnabledModulesJson);
        if (!modules.Contains("*") && !modules.Contains(moduleCode, StringComparer.OrdinalIgnoreCase))
            throw new TenantEntitlementException(
                $"The '{moduleCode}' module is not included in the company's current plan. Upgrade or contact the platform administrator.");
    }

    public async Task EnsureCanAddEmployeeAsync(int companyId)
    {
        var (_, plan) = await GetCurrentAsync(companyId);
        if (!plan.MaxActiveEmployees.HasValue) return;

        var current = await _db.Users.CountAsync(u => u.CompanyId == companyId && u.IsActive);
        if (current >= plan.MaxActiveEmployees.Value)
            throw new TenantEntitlementException(
                $"The current plan supports {plan.MaxActiveEmployees.Value} active employees. Upgrade the subscription to add more.",
                StatusCodes.Status409Conflict);
    }

    public async Task EnsureAiRequestCapacityAsync(int companyId)
    {
        var (_, plan) = await GetCurrentAsync(companyId);
        if (!plan.MaxMonthlyAiRequests.HasValue) return;

        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var used = await _db.TenantUsageEvents.AsNoTracking()
            .Where(e => e.CompanyId == companyId &&
                        e.MeterKey == "ai_request" &&
                        e.OccurredAt >= start &&
                        e.OccurredAt < end)
            .SumAsync(e => (decimal?)e.Quantity) ?? 0m;

        if (used >= plan.MaxMonthlyAiRequests.Value)
            throw new TenantEntitlementException(
                $"The company has reached its monthly AI request limit of {plan.MaxMonthlyAiRequests.Value}. Upgrade the plan or wait for the next billing period.",
                StatusCodes.Status429TooManyRequests);
    }



    public async Task ConsumeAiRequestAsync(
        int companyId,
        string idempotencyKey,
        string? resourceType = null,
        string? resourceId = null)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        if (await _db.TenantUsageEvents.AnyAsync(e =>
                e.CompanyId == companyId && e.IdempotencyKey == idempotencyKey))
        {
            await transaction.CommitAsync();
            return;
        }

        var (_, plan) = await GetCurrentAsync(companyId);
        if (plan.MaxMonthlyAiRequests.HasValue)
        {
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var used = await _db.TenantUsageEvents.AsNoTracking()
                .Where(e => e.CompanyId == companyId &&
                            e.MeterKey == "ai_request" &&
                            e.OccurredAt >= start &&
                            e.OccurredAt < end)
                .SumAsync(e => (decimal?)e.Quantity) ?? 0m;

            if (used >= plan.MaxMonthlyAiRequests.Value)
                throw new TenantEntitlementException(
                    $"The company has reached its monthly AI request limit of {plan.MaxMonthlyAiRequests.Value}. Upgrade the plan or wait for the next billing period.",
                    StatusCodes.Status429TooManyRequests);
        }

        await RecordUsageAsync(
            companyId,
            "ai_request",
            1,
            idempotencyKey,
            resourceType,
            resourceId);

        await transaction.CommitAsync();
    }

    public async Task EnsureStorageCapacityAsync(int companyId, long incomingBytes)
    {
        if (incomingBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(incomingBytes), "Incoming storage bytes must be positive.");

        var (_, plan) = await GetCurrentAsync(companyId);
        if (!plan.MaxStorageBytes.HasValue) return;

        var used = await _db.TenantUsageEvents.AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.MeterKey == "storage_bytes")
            .SumAsync(e => (decimal?)e.Quantity) ?? 0m;
        var safeUsed = Math.Max(0m, used);

        if (safeUsed + incomingBytes > plan.MaxStorageBytes.Value)
            throw new TenantEntitlementException(
                $"The company has reached its storage limit of {FormatBytes(plan.MaxStorageBytes.Value)}. Remove files or upgrade the plan.",
                StatusCodes.Status413PayloadTooLarge);
    }

    public async Task RecordUsageAsync(
        int companyId,
        string meterKey,
        decimal quantity,
        string idempotencyKey,
        string? resourceType = null,
        string? resourceId = null,
        object? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(meterKey) || meterKey.Length > 60)
            throw new ArgumentException("Meter key must be between 1 and 60 characters.", nameof(meterKey));
        var normalizedMeterKey = meterKey.Trim().ToLowerInvariant();
        if (quantity == 0 || (quantity < 0 && normalizedMeterKey != "storage_bytes"))
            throw new ArgumentOutOfRangeException(nameof(quantity), "Usage quantity must be positive, except storage_bytes may use a negative delta for deletion.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 160)
            throw new ArgumentException("Idempotency key must be between 1 and 160 characters.", nameof(idempotencyKey));

        if (await _db.TenantUsageEvents.AnyAsync(e => e.CompanyId == companyId && e.IdempotencyKey == idempotencyKey))
            return;

        var usageEvent = new TenantUsageEvent
        {
            CompanyId = companyId,
            MeterKey = normalizedMeterKey,
            Quantity = quantity,
            IdempotencyKey = idempotencyKey,
            ResourceType = resourceType,
            ResourceId = resourceId,
            MetadataJson = metadata == null ? null : JsonSerializer.Serialize(metadata),
            OccurredAt = DateTime.UtcNow
        };
        _db.TenantUsageEvents.Add(usageEvent);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(usageEvent).State = EntityState.Detached;
            // Unique (CompanyId, IdempotencyKey) makes retrying a metering event safe.
            if (!await _db.TenantUsageEvents.AnyAsync(e => e.CompanyId == companyId && e.IdempotencyKey == idempotencyKey))
                throw;
        }
    }

    public async Task<object> GetUsageSnapshotAsync(int companyId)
    {
        var (subscription, plan) = await GetCurrentAsync(companyId);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);

        var monthlyEvents = await _db.TenantUsageEvents.AsNoTracking()
            .Where(e => e.CompanyId == companyId &&
                        e.MeterKey != "storage_bytes" &&
                        e.OccurredAt >= monthStart &&
                        e.OccurredAt < nextMonth)
            .GroupBy(e => e.MeterKey)
            .Select(g => new { Meter = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var storageBytes = await _db.TenantUsageEvents.AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.MeterKey == "storage_bytes")
            .SumAsync(e => (decimal?)e.Quantity) ?? 0m;

        var activeEmployees = await _db.Users.AsNoTracking()
            .CountAsync(u => u.CompanyId == companyId && u.IsActive);
        var aiRequests = monthlyEvents.FirstOrDefault(e => e.Meter == "ai_request")?.Quantity ?? 0m;

        return new
        {
            Subscription = new
            {
                subscription.Status,
                Plan = plan.Name,
                PlanCode = plan.Code,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd,
                subscription.TrialEndsAt,
                subscription.CancelAtPeriodEnd,
                plan.BillingMode,
                plan.PriceMinorUnits,
                plan.CurrencyCode
            },
            Plan = new
            {
                plan.Code,
                plan.Name,
                plan.Description,
                plan.EnabledModulesJson
            },
            Limits = new
            {
                plan.MaxActiveEmployees,
                plan.MaxStorageBytes,
                plan.MaxMonthlyAiRequests
            },
            Usage = new
            {
                ActiveEmployees = activeEmployees,
                StorageBytes = Math.Max(0m, storageBytes),
                AiRequests = aiRequests,
                Events = monthlyEvents
            },
            GeneratedAt = now
        };
    }

    private static string FormatBytes(long value)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var amount = (double)value;
        var unit = 0;
        while (amount >= 1024 && unit < units.Length - 1)
        {
            amount /= 1024;
            unit++;
        }
        return $"{amount:0.#} {units[unit]}";
    }

    private static List<string> ParseModules(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new TenantEntitlementException(
                "The company's subscription plan has invalid module configuration. Contact the platform administrator.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static void EnsureSubscriptionActive(CompanySubscription subscription, SubscriptionPlan plan)
    {
        var now = DateTime.UtcNow;
        if (!plan.IsActive)
            throw new TenantEntitlementException("The company's subscription plan is inactive.", StatusCodes.Status402PaymentRequired);

        if (subscription.Status is "cancelled" or "suspended")
            throw new TenantEntitlementException("The company's subscription is not active.", StatusCodes.Status402PaymentRequired);

        if (subscription.Status == "trialing" && subscription.TrialEndsAt.HasValue && subscription.TrialEndsAt.Value <= now)
            throw new TenantEntitlementException("The company's trial period has ended.", StatusCodes.Status402PaymentRequired);

        if (subscription.Status == "past_due" && subscription.CurrentPeriodEnd <= now)
            throw new TenantEntitlementException("The company's subscription is past due. Update billing to continue.", StatusCodes.Status402PaymentRequired);
    }
}
