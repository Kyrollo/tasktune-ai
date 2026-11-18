#nullable enable
namespace TaskTune.Infrastructure.Models;
public sealed class TenantSubscription
{
    public int SubscriptionId { get; set; }
    public int CompanyId { get; set; }
    public int PlanId { get; set; }
    public string Status { get; set; } = "Trial";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Company Company { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
}
