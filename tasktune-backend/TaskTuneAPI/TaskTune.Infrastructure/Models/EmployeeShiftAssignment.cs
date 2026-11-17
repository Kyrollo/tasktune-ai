namespace TaskTune.Infrastructure.Models;

public partial class EmployeeShiftAssignment
{
    public int EmployeeShiftAssignmentId { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public int WorkShiftId { get; set; }
    public byte DaysOfWeekMask { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
