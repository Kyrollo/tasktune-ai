namespace TaskTune.Infrastructure.Models;

public partial class EmployeeReportingLine
{
    public int ReportingLineId { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeUserId { get; set; }
    public int ManagerUserId { get; set; }
    public string RelationshipType { get; set; } = "line";
    public bool IsPrimary { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
