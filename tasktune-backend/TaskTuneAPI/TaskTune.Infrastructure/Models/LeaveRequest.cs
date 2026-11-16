namespace TaskTune.Infrastructure.Models;

public partial class LeaveRequest
{
    public int LeaveRequestId { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public int LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal RequestedDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int? ApproverUserId { get; set; }
    public int? DecisionBy { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
