namespace TaskTune.Infrastructure.Models;

public partial class SubscriptionPlan
{
    public int SubscriptionPlanId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? MaxActiveEmployees { get; set; }
    public long? MaxStorageBytes { get; set; }
    public int? MaxMonthlyAiRequests { get; set; }
    public string EnabledModulesJson { get; set; } = "[]";
    public string BillingMode { get; set; } = "contact_sales";
    public int? PriceMinorUnits { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
