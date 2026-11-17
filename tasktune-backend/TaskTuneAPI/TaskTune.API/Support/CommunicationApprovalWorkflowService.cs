using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;
using Task = System.Threading.Tasks.Task;

namespace TaskTune.API.Support;

public sealed record CommunicationApprovalResult(
    bool Success,
    string? Error = null,
    int? ApprovalId = null,
    int QueuedCount = 0);

public sealed class CommunicationApprovalWorkflowService(TaskTuneDbContext db)
{
    public async Task<CommunicationApprovalResult> ApproveAsync(
        int companyId,
        int approvalId,
        int reviewerId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (note?.Length > 1000)
            return new(false, "The approval note cannot exceed 1,000 characters.");

        var approval = await db.CommunicationApprovals
            .Include(a => a.OutboxItems)
            .FirstOrDefaultAsync(a => a.ApprovalId == approvalId && a.CompanyId == companyId, cancellationToken);

        if (approval == null)
            return new(false, "Approval request was not found.");
        if (approval.RequestedByUserId == reviewerId)
            return new(false, "A communication request cannot be approved by its requester.");
        if (!string.Equals(approval.Status, "PendingApproval", StringComparison.Ordinal))
            return new(false, "This approval request has already been reviewed.");

        var pendingItems = approval.OutboxItems
            .Where(item => item.CompanyId == companyId &&
                           string.Equals(item.Status, "PendingApproval", StringComparison.Ordinal))
            .ToList();
        if (pendingItems.Count == 0)
            return new(false, "The approval request has no pending recipients.");

        var now = DateTime.UtcNow;
        approval.Status = "Approved";
        approval.ReviewedByUserId = reviewerId;
        approval.ReviewedAt = now;
        approval.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        foreach (var item in pendingItems)
        {
            item.Status = "Queued";
            item.AvailableAt = now;
            item.LockedUntil = null;
            item.LockId = null;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(true, ApprovalId: approvalId, QueuedCount: pendingItems.Count);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(false, "This approval was changed by another reviewer. Refresh and try again.");
        }
    }

    public async Task<CommunicationApprovalResult> RejectAsync(
        int companyId,
        int approvalId,
        int reviewerId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (note?.Length > 1000)
            return new(false, "The review note cannot exceed 1,000 characters.");

        var approval = await db.CommunicationApprovals
            .Include(a => a.OutboxItems)
            .FirstOrDefaultAsync(a => a.ApprovalId == approvalId && a.CompanyId == companyId, cancellationToken);

        if (approval == null)
            return new(false, "Approval request was not found.");
        if (approval.RequestedByUserId == reviewerId)
            return new(false, "A communication request cannot be rejected by its requester.");
        if (!string.Equals(approval.Status, "PendingApproval", StringComparison.Ordinal))
            return new(false, "This approval request has already been reviewed.");

        var now = DateTime.UtcNow;
        approval.Status = "Rejected";
        approval.ReviewedByUserId = reviewerId;
        approval.ReviewedAt = now;
        approval.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        foreach (var item in approval.OutboxItems.Where(item =>
                     item.CompanyId == companyId &&
                     string.Equals(item.Status, "PendingApproval", StringComparison.Ordinal)))
        {
            item.Status = "Rejected";
            item.LockedUntil = null;
            item.LockId = null;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(true, ApprovalId: approvalId);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(false, "This approval was changed by another reviewer. Refresh and try again.");
        }
    }
}
