using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum CommunicationApprovalResult
{
    Success,
    NotFound,
    CannotReviewOwnRequest,
    AlreadyReviewed,
    InvalidRecipients
}

public sealed class CommunicationApprovalService(TaskTuneDbContext db)
{
    public async System.Threading.Tasks.Task<CommunicationApprovalResult> ApproveAsync(
        int approvalId, int companyId, int reviewerId, CancellationToken cancellationToken = default)
    {
        var approval = await db.CommunicationApprovals
            .SingleOrDefaultAsync(a => a.ApprovalId == approvalId && a.CompanyId == companyId, cancellationToken);
        if (approval == null) return CommunicationApprovalResult.NotFound;
        if (approval.Status != "Pending") return CommunicationApprovalResult.AlreadyReviewed;
        if (approval.RequestedByUserId == reviewerId) return CommunicationApprovalResult.CannotReviewOwnRequest;

        List<string> recipients;
        List<string> cc;
        try
        {
            recipients = JsonSerializer.Deserialize<List<string>>(approval.RecipientEmailsJson) ?? [];
            cc = JsonSerializer.Deserialize<List<string>>(approval.CcEmailsJson) ?? [];
        }
        catch (JsonException)
        {
            return CommunicationApprovalResult.InvalidRecipients;
        }

        recipients = recipients.Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count == 0 || recipients.Count > 1000)
            return CommunicationApprovalResult.InvalidRecipients;

        var now = DateTime.UtcNow;
        var outboxRows = recipients.Select(email => new CommunicationOutbox
        {
            CompanyId = companyId,
            ApprovalId = approval.ApprovalId,
            Approval = approval,
            RecipientEmail = email,
            CcEmailsJson = JsonSerializer.Serialize(cc),
            Subject = approval.Subject,
            Body = approval.Body,
            Status = "Pending",
            Attempts = 0,
            NextAttemptAt = now,
            CreatedAt = now,
            IdempotencyKey = CreateDeliveryKey(approval.ApprovalId, email)
        }).ToList();

        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerId;
        approval.ReviewedAt = now;
        db.CommunicationOutbox.AddRange(outboxRows);
        try
        {
            // A single SaveChanges is transactional on relational providers, so the
            // approval transition and every recipient outbox row succeed together.
            await db.SaveChangesAsync(cancellationToken);
            return CommunicationApprovalResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommunicationApprovalResult.AlreadyReviewed;
        }
    }

    public async System.Threading.Tasks.Task<CommunicationApprovalResult> RejectAsync(
        int approvalId, int companyId, int reviewerId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 1000)
            return CommunicationApprovalResult.InvalidRecipients;
        var approval = await db.CommunicationApprovals
            .SingleOrDefaultAsync(a => a.ApprovalId == approvalId && a.CompanyId == companyId, cancellationToken);
        if (approval == null) return CommunicationApprovalResult.NotFound;
        if (approval.Status != "Pending") return CommunicationApprovalResult.AlreadyReviewed;
        if (approval.RequestedByUserId == reviewerId) return CommunicationApprovalResult.CannotReviewOwnRequest;

        approval.Status = "Rejected";
        approval.ReviewedByUserId = reviewerId;
        approval.ReviewedAt = DateTime.UtcNow;
        approval.RejectionReason = reason.Trim();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return CommunicationApprovalResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommunicationApprovalResult.AlreadyReviewed;
        }
    }

    private static string CreateDeliveryKey(int approvalId, string recipient)
    {
        var canonical = approvalId.ToString() + ":" + recipient.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
