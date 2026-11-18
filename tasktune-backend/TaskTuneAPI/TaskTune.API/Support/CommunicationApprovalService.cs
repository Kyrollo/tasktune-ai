using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public enum CommunicationApprovalResult
{
    Success,
    NotFound,
    CannotReviewOwnRequest,
    AlreadyReviewed,
    InvalidRecipients,
    RecipientQuotaExceeded,
    TenantQueueQuotaExceeded
}

public sealed class CommunicationApprovalService(TaskTuneDbContext db, IConfiguration? configuration = null)
{
    public async System.Threading.Tasks.Task<CommunicationApprovalResult> ApproveAsync(
        int approvalId, int companyId, int reviewerId, CancellationToken cancellationToken = default)
    {
        var maxRecipients = Math.Clamp(configuration?.GetValue<int?>("Email:Outbox:MaxRecipientsPerApproval") ?? 250, 1, 1000);
        var maxPendingPerTenant = Math.Clamp(configuration?.GetValue<int?>("Email:Outbox:MaxPendingPerTenant") ?? 1000, 1, 100000);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

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
        if (recipients.Count == 0)
            return CommunicationApprovalResult.InvalidRecipients;
        if (recipients.Count > maxRecipients)
            return CommunicationApprovalResult.RecipientQuotaExceeded;

        // The SERIALIZABLE transaction protects this tenant backlog check from concurrent approvals.
        var activeBacklog = await db.CommunicationOutbox
            .Where(item => item.CompanyId == companyId &&
                (item.Status == "Pending" || item.Status == "Processing"))
            .LongCountAsync(cancellationToken);
        if (activeBacklog + recipients.Count > maxPendingPerTenant)
            return CommunicationApprovalResult.TenantQueueQuotaExceeded;

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
        db.Notifications.Add(new Notification
        {
            UserId = approval.RequestedByUserId,
            NotificationType = 0,
            Title = "Communication approved",
            Message = $"Your communication request \"{approval.Subject}\" was approved and queued for {recipients.Count} recipient(s). Delivery is pending.",
            IsRead = false,
            SentAt = now
        });
        db.CommunicationOutbox.AddRange(outboxRows);
        try
        {
            // A single SaveChanges is transactional on relational providers, so the
            // approval transition and every recipient outbox row succeed together.
            await db.SaveChangesAsync(cancellationToken);
            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
            return CommunicationApprovalResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            return CommunicationApprovalResult.AlreadyReviewed;
        }
        catch (DbUpdateException)
        {
            // A simultaneous reviewer can win the approval transition. SQL Server
            // rolls back the approval + recipient outbox transaction on unique/FK errors.
            if (transaction != null)
                await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var latestStatus = await db.CommunicationApprovals.AsNoTracking()
                .Where(a => a.ApprovalId == approvalId && a.CompanyId == companyId)
                .Select(a => a.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (latestStatus != null && latestStatus != "Pending")
                return CommunicationApprovalResult.AlreadyReviewed;
            if (latestStatus == null)
                return CommunicationApprovalResult.NotFound;
            throw;
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
        db.Notifications.Add(new Notification
        {
            UserId = approval.RequestedByUserId,
            NotificationType = 0,
            Title = "Communication rejected",
            Message = $"Your communication request \"{approval.Subject}\" was rejected. Reason: {reason.Trim()}",
            IsRead = false,
            SentAt = approval.ReviewedAt.Value
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return CommunicationApprovalResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommunicationApprovalResult.AlreadyReviewed;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var latestStatus = await db.CommunicationApprovals.AsNoTracking()
                .Where(a => a.ApprovalId == approvalId && a.CompanyId == companyId)
                .Select(a => a.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (latestStatus != null && latestStatus != "Pending")
                return CommunicationApprovalResult.AlreadyReviewed;
            if (latestStatus == null)
                return CommunicationApprovalResult.NotFound;
            throw;
        }
    }

    private static string CreateDeliveryKey(int approvalId, string recipient)
    {
        var canonical = approvalId.ToString() + ":" + recipient.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
