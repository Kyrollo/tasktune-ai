#nullable enable
namespace TaskTune.Infrastructure.Models;

/// <summary>
/// Append-only, tenant-scoped usage record. AI token quantities are written only when a provider
/// or trusted server adapter reports an actual usage count.
/// </summary>
public sealed class TenantUsageEvent
{
    public long EventId { get; set; }
    public int CompanyId { get; set; }
    public string MeterCode { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
