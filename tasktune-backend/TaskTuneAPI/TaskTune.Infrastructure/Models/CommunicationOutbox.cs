#nullable enable
namespace TaskTune.Infrastructure.Models;

public sealed class CommunicationOutbox
{
    public long OutboxId { get; set; }
    public int CompanyId { get; set; }
    public int ApprovalId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string CcEmailsJson { get; set; } = "[]";
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public CommunicationApproval Approval { get; set; } = null!;
}
