namespace TaskTune.Infrastructure.Models;

public partial class AttendanceRecord
{
    public int AttendanceRecordId { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public int WorkShiftId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime ScheduledStartUtc { get; set; }
    public DateTime ScheduledEndUtc { get; set; }
    public DateTime? CheckInUtc { get; set; }
    public DateTime? CheckOutUtc { get; set; }
    public string Status { get; set; } = "scheduled";
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public int WorkedMinutes { get; set; }
    public int BreakMinutes { get; set; }
    public string CaptureSource { get; set; } = "web";
    public string? Notes { get; set; }
    public bool IsManualAdjustment { get; set; }
    public int? AdjustedBy { get; set; }
    public DateTime? AdjustedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
