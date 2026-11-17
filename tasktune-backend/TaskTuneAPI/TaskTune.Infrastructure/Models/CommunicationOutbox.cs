namespace TaskTune.Infrastructure.Models;

public class CommunicationOutbox
{
    public long OutboxId { get; set; }
    public int CompanyId { get; set; }
    public int ApprovalId { get; set; }
    public int CommunicationId { get; set; }
    public int RecipientUserId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? CcEmailsJson { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingApproval";
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public Guid? LockId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public Guid IdempotencyKey { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public virtual Company Company { get; set; } = null!;
    public virtual CommunicationApproval Approval { get; set; } = null!;
    public virtual CommunicationLog Communication { get; set; } = null!;
    public virtual User RecipientUser { get; set; } = null!;
}
