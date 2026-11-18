#nullable enable
namespace TaskTune.Infrastructure.Models;

public partial class Company
{
    public string Status { get; set; } = "Active";
    public DateTime? SuspendedAt { get; set; }
}
