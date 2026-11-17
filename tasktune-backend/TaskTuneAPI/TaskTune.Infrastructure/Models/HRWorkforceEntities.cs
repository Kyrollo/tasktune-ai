using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTune.Infrastructure.Models;

[Table("JobApplicants")]
public sealed class JobApplicant
{
    [Key] public int JobApplicantId { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(160), Required] public string CandidateName { get; set; } = "";
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(40)] public string Phone { get; set; } = "";
    [MaxLength(160), Required] public string JobTitle { get; set; } = "";
    [MaxLength(80)] public string Source { get; set; } = "direct";
    [MaxLength(24)] public string Stage { get; set; } = "applied";
    [MaxLength(2000)] public string Notes { get; set; } = "";
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("OnboardingTasks")]
public sealed class OnboardingTask
{
    [Key] public int OnboardingTaskId { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeUserId { get; set; }
    [MaxLength(180), Required] public string Title { get; set; } = "";
    [MaxLength(2000)] public string Description { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "open";
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

[Table("PerformanceGoals")]
public sealed class PerformanceGoal
{
    [Key] public int PerformanceGoalId { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    [MaxLength(180), Required] public string Title { get; set; } = "";
    [MaxLength(2000)] public string Description { get; set; } = "";
    [MaxLength(100)] public string MetricName { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal TargetValue { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentValue { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "active";
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("ExpenseClaims")]
public sealed class ExpenseClaim
{
    [Key] public int ExpenseClaimId { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    [MaxLength(100), Required] public string Category { get; set; } = "";
    [MaxLength(1800)] public string Description { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Column(TypeName = "char(3)"), MaxLength(3)] public string CurrencyCode { get; set; } = "USD";
    public DateOnly ExpenseDate { get; set; }
    [MaxLength(500)] public string? ReceiptReference { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "pending";
    [MaxLength(1200)] public string? ReviewComment { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("CompanyAssets")]
public sealed class CompanyAsset
{
    [Key] public int CompanyAssetId { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(80), Required] public string AssetTag { get; set; } = "";
    [MaxLength(180), Required] public string Name { get; set; } = "";
    [MaxLength(100)] public string Category { get; set; } = "";
    [MaxLength(160)] public string? SerialNumber { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "available";
    public int? AssignedToUserId { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("BenefitPlans")]
public sealed class BenefitPlan
{
    [Key] public int BenefitPlanId { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(180), Required] public string Name { get; set; } = "";
    [MaxLength(180)] public string ProviderName { get; set; } = "";
    [MaxLength(1200)] public string Description { get; set; } = "";
    [Column(TypeName = "decimal(18,2)")] public decimal EmployeeContribution { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal EmployerContribution { get; set; }
    [Column(TypeName = "char(3)"), MaxLength(3)] public string CurrencyCode { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Table("BenefitEnrollments")]
public sealed class BenefitEnrollment
{
    [Key] public int BenefitEnrollmentId { get; set; }
    public int CompanyId { get; set; }
    public int BenefitPlanId { get; set; }
    public int UserId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "pending";
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedBy { get; set; }
}
