#nullable enable
namespace TaskTune.Infrastructure.Models;
public sealed class SubscriptionPlanFeature
{
    public int PlanId { get; set; }
    public string FeatureCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int? LimitValue { get; set; }
}
