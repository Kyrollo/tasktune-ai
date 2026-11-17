namespace TaskTune.Infrastructure.Models;

public partial class CommunicationOutbox
{
    public Guid OutboxId { get; set; }
    public Guid ApprovalId { get; set; }
    public int CompanyId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string CcEmailsJson { get; set; } = "[]";
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public byte Status { get; set; }
    public byte AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public virtual CommunicationApproval Approval { get; set; } = null!;
}
