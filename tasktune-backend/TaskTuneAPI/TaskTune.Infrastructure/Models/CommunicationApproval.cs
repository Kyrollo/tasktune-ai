namespace TaskTune.Infrastructure.Models;

public class CommunicationApproval
{
    public int ApprovalId { get; set; }
    public Guid BatchId { get; set; }
    public int CompanyId { get; set; }
    public int RequestedByUserId { get; set; }
    public string Status { get; set; } = "PendingApproval";
    public DateTime RequestedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public virtual Company Company { get; set; } = null!;
    public virtual User RequestedBy { get; set; } = null!;
    public virtual User? ReviewedBy { get; set; }
    public virtual ICollection<CommunicationOutbox> OutboxItems { get; set; } = new List<CommunicationOutbox>();
}
