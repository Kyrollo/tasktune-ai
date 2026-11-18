#nullable enable
namespace TaskTune.Infrastructure.Models;

public sealed class TenantLifecycleEvent
{
    public long EventId { get; set; }
    public int CompanyId { get; set; }
    public int ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Company Company { get; set; } = null!;
}
