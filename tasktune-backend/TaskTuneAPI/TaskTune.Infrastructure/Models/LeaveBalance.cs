namespace TaskTune.Infrastructure.Models;

public partial class LeaveBalance
{
    public int LeaveBalanceId { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal AllocatedDays { get; set; }
    public decimal CarriedOverDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal PendingDays { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
