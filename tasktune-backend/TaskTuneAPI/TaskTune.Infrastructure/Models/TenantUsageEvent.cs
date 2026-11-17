namespace TaskTune.Infrastructure.Models;

public partial class TenantUsageEvent
{
    public long TenantUsageEventId { get; set; }
    public int CompanyId { get; set; }
    public string MeterKey { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime OccurredAt { get; set; }
}
