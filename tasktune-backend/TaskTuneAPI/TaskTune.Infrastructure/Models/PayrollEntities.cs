using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskTune.Infrastructure.Models;

[Table("PayrollPeriods")]
public sealed class PayrollPeriod
{
    [Key] public int PayrollPeriodId { get; set; }
    public int CompanyId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    [Column(TypeName = "char(3)"), MaxLength(3)] public string CurrencyCode { get; set; } = "EGP";
    [MaxLength(20)] public string Status { get; set; } = "draft";
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedBy { get; set; }
}

[Table("Payslips")]
public sealed class Payslip
{
    [Key] public int PayslipId { get; set; }
    public int CompanyId { get; set; }
    public int PayrollPeriodId { get; set; }
    public int UserId { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal BaseSalary { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Allowances { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Bonus { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal IncomeTaxDeduction { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SocialInsuranceDeduction { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OtherDeductions { get; set; }
    [MaxLength(3)] public string CurrencyCode { get; set; } = "EGP";
    [MaxLength(20)] public string Status { get; set; } = "draft";
    [MaxLength(1500)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? PaidBy { get; set; }
}
