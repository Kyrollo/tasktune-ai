namespace TaskTune.Infrastructure.Models;

public partial class LeaveRequestBalanceAllocation
{
    public int LeaveRequestBalanceAllocationId { get; set; }
    public int CompanyId { get; set; }
    public int LeaveRequestId { get; set; }
    public int LeaveBalanceId { get; set; }
    public int LeaveYear { get; set; }
    public decimal AllocatedDays { get; set; }
    public DateTime CreatedAt { get; set; }
}
