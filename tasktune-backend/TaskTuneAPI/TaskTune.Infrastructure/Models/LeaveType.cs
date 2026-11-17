namespace TaskTune.Infrastructure.Models;

public partial class LeaveType
{
    public int LeaveTypeId { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPaid { get; set; }
    public bool RequiresAttachment { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public decimal AnnualAllowanceDays { get; set; }
    public decimal MaximumConsecutiveDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
