#nullable enable
namespace TaskTune.Infrastructure.Models;
public sealed class SubscriptionPlan
{
    public int PlanId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int? MaxUsers { get; set; }
    public long? MaxStorageBytes { get; set; }
    public long? MonthlyAiTokenLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
