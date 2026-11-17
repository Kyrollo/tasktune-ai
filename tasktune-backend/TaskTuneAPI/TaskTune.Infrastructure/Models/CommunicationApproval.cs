namespace TaskTune.Infrastructure.Models;

public partial class CommunicationApproval
{
    public Guid ApprovalId { get; set; }
    public int CompanyId { get; set; }
    public int CommunicationId { get; set; }
    public int RequestedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public int RecipientCount { get; set; }
    public byte Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public virtual CommunicationLog Communication { get; set; } = null!;
    public virtual User RequestedByNavigation { get; set; } = null!;
    public virtual User? ReviewedByNavigation { get; set; }
    public virtual ICollection<CommunicationOutbox> OutboxItems { get; set; } = new List<CommunicationOutbox>();
}

public static class CommunicationApprovalStatus
{
    public const byte Pending = 0;
    public const byte Approved = 1;
    public const byte Sent = 2;
    public const byte Rejected = 3;
    public const byte Failed = 4;
    public const byte Partial = 5;
    public const byte NeedsManualReview = 6;
}

public static class CommunicationOutboxStatus
{
    public const byte Pending = 0;
    public const byte Sending = 1;
    public const byte Sent = 2;
    public const byte Failed = 3;
}
