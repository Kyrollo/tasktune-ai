#nullable enable
namespace TaskTune.Infrastructure.Models;

public sealed class CommunicationApproval
{
    public int ApprovalId { get; set; }
    public int CompanyId { get; set; }
    public int RequestedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public byte CommunicationType { get; set; }
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientEmailsJson { get; set; } = "[]";
    public string CcEmailsJson { get; set; } = "[]";
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public Guid IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Company Company { get; set; } = null!;
    public User RequestedByUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
