namespace TaskTune.Infrastructure.Models;

public partial class CompanySubscription
{
    public int CompanySubscriptionId { get; set; }
    public int CompanyId { get; set; }
    public int SubscriptionPlanId { get; set; }
    public string Status { get; set; } = "active";
    public DateTime? TrialEndsAt { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? ExternalCustomerId { get; set; }
    public string? ExternalSubscriptionId { get; set; }
    public string? BillingProvider { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
