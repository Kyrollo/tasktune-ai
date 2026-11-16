namespace TaskTune.Infrastructure.Models;

public partial class CompanyHoliday
{
    public int CompanyHolidayId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly HolidayDate { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
