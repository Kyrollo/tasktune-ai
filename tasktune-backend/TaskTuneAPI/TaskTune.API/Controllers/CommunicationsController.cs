using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/communications")]
[Authorize]
public class CommunicationsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly CommunicationApprovalWorkflowService _approvalWorkflow;

    public CommunicationsController(
        TaskTuneDbContext db,
        IConfiguration configuration,
        CommunicationApprovalWorkflowService approvalWorkflow)
    {
        _db = db;
        _configuration = configuration;
        _approvalWorkflow = approvalWorkflow;
    }

    [HttpGet]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> GetCommunications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.CommunicationLogs
            .Include(c => c.SentByNavigation)
            .Include(c => c.RecipientUser)
            .Include(c => c.RecipientDepartment)
            .Where(c => c.CompanyId == companyId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.CommunicationId,
                c.CompanyId,
                c.SentBy,
                SentByName = c.SentByNavigation != null ? (c.SentByNavigation.FullName ?? c.SentByNavigation.FirstName + " " + c.SentByNavigation.LastName) : null,
                c.RecipientUserId,
                RecipientUserName = c.RecipientUser != null ? (c.RecipientUser.FullName ?? c.RecipientUser.FirstName + " " + c.RecipientUser.LastName) : null,
                RecipientUserEmail = c.RecipientUser != null ? c.RecipientUser.Email : null,
                c.RecipientDepartmentId,
                RecipientDepartmentName = c.RecipientDepartment != null ? c.RecipientDepartment.DepartmentName : null,
                c.CommunicationType,
                c.Subject,
                c.Body,
                c.SentAt,
                DeliveryStatus = _db.CommunicationOutbox
                    .Where(outbox => outbox.CommunicationId == c.CommunicationId &&
                                     outbox.CompanyId == companyId.Value)
                    .Select(outbox => outbox.Status)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new
        {
            items = items.Select(c => new
            {
                c.CommunicationId,
                id = c.CommunicationId,
                c.CompanyId,
                c.SentBy,
                c.SentByName,
                c.RecipientUserId,
                c.RecipientUserName,
                c.RecipientUserEmail,
                c.RecipientDepartmentId,
                c.RecipientDepartmentName,
                c.CommunicationType,
                type = GetCommunicationTypeName(c.CommunicationType),
                recipientType = c.RecipientUserId.HasValue ? "Individual" : c.RecipientDepartmentId.HasValue ? "Department" : "Company",
                recipient = c.RecipientUserName ?? c.RecipientDepartmentName ?? "All Employees",
                c.Subject,
                message = c.Body,
                body = c.Body,
                status = GetDeliveryStatus(c.DeliveryStatus),
                sentAt = c.SentAt,
                date = c.SentAt
            }),
            totalCount = total
        });
    }

    [HttpGet("overview")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> GetOverview()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });
        var aiConfig = await AIConfigurationStore.LoadAsync(_db, companyId.Value, _configuration);
        var agentConfig = aiConfig.GetAgent("email_assistant");

        var users = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.Position)
            .Where(u => u.CompanyId == companyId.Value && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new
            {
                u.UserId,
                FullName = u.FullName ?? (u.FirstName + " " + u.LastName),
                u.Email,
                DepartmentName = u.Department != null ? u.Department.DepartmentName : null,
                PositionTitle = u.Position != null ? u.Position.PositionTitle : null,
                u.Role
            })
            .ToListAsync();

        var departments = await _db.Departments
            .Where(d => d.CompanyId == companyId.Value)
            .OrderBy(d => d.DepartmentName)
            .Select(d => new
            {
                d.DepartmentId,
                d.DepartmentName,
                EmployeeCount = d.Users.Count(u => u.IsActive)
            })
            .ToListAsync();

        var recentCount = await _db.CommunicationOutbox.CountAsync(item =>
            item.CompanyId == companyId.Value && item.Status == "Sent");
        var pendingCount = await _db.CommunicationApprovals.CountAsync(item =>
            item.CompanyId == companyId.Value && item.Status == "PendingApproval");
        var queuedCount = await _db.CommunicationOutbox.CountAsync(item =>
            item.CompanyId == companyId.Value && item.Status is "Queued" or "Processing");
        var lastSentAt = await _db.CommunicationOutbox
            .Where(item => item.CompanyId == companyId.Value && item.Status == "Sent")
            .OrderByDescending(item => item.SentAt)
            .Select(item => item.SentAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            agent = new
            {
                name = agentConfig.Name,
                enabled = aiConfig.Enabled && aiConfig.Features.EmailAssistant && agentConfig.Enabled,
                senderEmail = Environment.GetEnvironmentVariable("GMAIL_USER"),
                mode = "smtp",
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                baseUrl = agentConfig.BaseUrl,
                endpoint = agentConfig.Endpoint
            },
            users,
            departments,
            templates = GetTemplates(),
            stats = new
            {
                totalSent = recentCount,
                scheduled = queuedCount,
                pendingApproval = pendingCount,
                lastSentAt
            }
        });
    }

    [HttpPost("recommendation")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> GenerateRecommendation([FromBody] CommunicationRecommendationRequest request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null) return Unauthorized();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });

        var sender = await _db.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);
        if (sender == null) return Unauthorized();
        var aiConfig = await AIConfigurationStore.LoadAsync(_db, companyId.Value, _configuration);
        var agentConfig = aiConfig.GetAgent("email_assistant");
        if (!aiConfig.Enabled || !aiConfig.Features.EmailAssistant || !agentConfig.Enabled)
        {
            return StatusCode(503, new { message = "Email assistant is disabled in AI Control Panel." });
        }

        var recipients = await ResolveRecipientsAsync(companyId.Value, request.RecipientType, request.RecipientUserId, request.RecipientUserIds, request.RecipientDepartmentId);
        var recipientSummary = recipients.Select(u => new
        {
            userId = u.UserId,
            name = u.FullName ?? (u.FirstName + " " + u.LastName),
            email = u.Email,
            department = u.Department != null ? u.Department.DepartmentName : "Unassigned"
        }).ToList();

        var template = GetTemplates().FirstOrDefault(t => string.Equals(t.key, request.TemplateKey, StringComparison.OrdinalIgnoreCase));
        var payload = new
        {
            communication_type = request.CommunicationType ?? request.Type ?? "Email",
            recipient_type = request.RecipientType ?? "Individual",
            template = template is null ? null : new { template.key, template.label, template.subject, template.body },
            current_subject = request.Subject ?? "",
            current_body = request.Body ?? "",
            prompt = request.Prompt ?? "",
            sender = new
            {
                name = sender.FullName ?? (sender.FirstName + " " + sender.LastName),
                email = sender.Email,
                role = sender.Role,
                department = sender.Department != null ? sender.Department.DepartmentName : "Unassigned"
            },
            recipients = recipientSummary,
            agent_config = new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                api_key = agentConfig.ApiKey,
                temperature = agentConfig.Temperature,
                max_tokens = agentConfig.MaxTokens
            }
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        if (!AgentServiceSecurity.TryConfigure(http, _configuration, agentConfig.BaseUrl))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Private AI agent authentication is not configured." });
        try
        {
            var agentUrl = string.IsNullOrWhiteSpace(agentConfig.BaseUrl)
                ? (Environment.GetEnvironmentVariable("AGENT_EMAIL_URL") ?? "http://localhost:8005")
                : agentConfig.BaseUrl;
            var endpoint = string.IsNullOrWhiteSpace(agentConfig.Endpoint) ? "/api/email/recommendation" : agentConfig.Endpoint;
            var response = await http.PostAsJsonAsync($"{agentUrl.TrimEnd('/')}{endpoint}", payload);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return StatusCode(502, new { message = "Email agent returned an error." });
            }

            var result = await response.Content.ReadFromJsonAsync<object>();
            _db.AIActionsLogs.Add(new AIActionsLog
            {
                ActionType = 5,
                UserId = userId.Value,
                InputData = JsonSerializer.Serialize(new
                {
                    payload.communication_type,
                    payload.recipient_type,
                    payload.template,
                    payload.current_subject,
                    payload.current_body,
                    payload.prompt,
                    payload.sender,
                    payload.recipients
                }),
                OutputData = JsonSerializer.Serialize(result),
                ModelVersion = agentConfig.Model,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Failed to reach AI email agent." });
        }
    }

    [HttpGet("approvals/pending")]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> GetPendingApprovals([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.CommunicationApprovals
            .Include(approval => approval.RequestedBy)
            .Include(approval => approval.OutboxItems)
            .Where(approval => approval.CompanyId == companyId.Value &&
                               approval.Status == "PendingApproval");

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(approval => approval.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(approval => new
            {
                approval.ApprovalId,
                approval.BatchId,
                approval.Status,
                approval.RequestedAt,
                RequestedByUserId = approval.RequestedByUserId,
                RequestedByName = approval.RequestedBy.FullName,
                RecipientCount = approval.OutboxItems.Count,
                Subject = approval.OutboxItems.Select(item => item.Subject).FirstOrDefault(),
                Preview = approval.OutboxItems.Select(item => item.Body).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new { items, totalCount = total });
    }

    [HttpPost("approvals/{approvalId:int}/approve")]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> ApproveCommunication(
        int approvalId,
        [FromBody] CommunicationReviewRequest? request,
        CancellationToken cancellationToken)
    {
        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();

        var result = await _approvalWorkflow.ApproveAsync(
            companyId.Value, approvalId, userId.Value, request?.Note, cancellationToken);
        if (!result.Success)
        {
            if (result.Error?.Contains("requester", StringComparison.OrdinalIgnoreCase) == true)
                return Forbid();
            if (result.Error == "Approval request was not found.")
                return NotFound(new { message = result.Error });
            return Conflict(new { message = result.Error });
        }

        await AuditLogWriter.WriteAsync(_db, HttpContext, userId,
            action: "APPROVE", resourceType: "CommunicationApproval",
            resourceId: approvalId,
            after: new { status = "Approved", queuedRecipients = result.QueuedCount });

        return Ok(new
        {
            approvalId,
            status = "Approved",
            queuedRecipients = result.QueuedCount,
            message = "Approved. Delivery has been queued."
        });
    }

    [HttpPost("approvals/{approvalId:int}/reject")]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> RejectCommunication(
        int approvalId,
        [FromBody] CommunicationReviewRequest? request,
        CancellationToken cancellationToken)
    {
        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();

        var result = await _approvalWorkflow.RejectAsync(
            companyId.Value, approvalId, userId.Value, request?.Note, cancellationToken);
        if (!result.Success)
        {
            if (result.Error?.Contains("requester", StringComparison.OrdinalIgnoreCase) == true)
                return Forbid();
            if (result.Error == "Approval request was not found.")
                return NotFound(new { message = result.Error });
            return Conflict(new { message = result.Error });
        }

        await AuditLogWriter.WriteAsync(_db, HttpContext, userId,
            action: "REJECT", resourceType: "CommunicationApproval",
            resourceId: approvalId,
            after: new { status = "Rejected", note = request?.Note });

        return Ok(new { approvalId, status = "Rejected" });
    }

    [HttpPost]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateCommunication(
        [FromBody] CreateCommunicationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null) return Unauthorized();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });
        if (request == null || string.IsNullOrWhiteSpace(request.Subject) ||
            request.Subject.Trim().Length > 200 || string.IsNullOrWhiteSpace(request.Body) ||
            request.Body.Trim().Length > 20000)
            return BadRequest(new { message = "Subject (1–200 characters) and body (1–20000 characters) are required." });

        var recipientType = request.RecipientType?.Trim() ?? "Individual";
        if (!new[] { "Individual", "Multiple Employees", "Department", "Company" }
                .Contains(recipientType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid recipient type." });

        var communicationType = request.CommunicationType ?? MapCommunicationType(request.Type);
        if (communicationType is < 1 or > 3)
            return BadRequest(new { message = "Invalid communication type." });

        var ccEmails = (request.CcEmails ?? [])
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ccEmails.Count > 25 || ccEmails.Any(email =>
            !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "CC addresses must be valid and cannot exceed 25 recipients." });

        if (ccEmails.Count > 0)
        {
            var companyCcEmails = await _db.Users
                .Where(user => user.CompanyId == companyId.Value && user.IsActive &&
                               user.Email != null && ccEmails.Contains(user.Email))
                .Select(user => user.Email!)
                .ToListAsync(cancellationToken);
            if (companyCcEmails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ccEmails.Count)
                return BadRequest(new { message = "CC recipients must be active users in your company." });
        }

        List<User> recipientUsers;
        Department? recipientDepartment = null;

        if (recipientType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
        {
            var recipient = await ResolveRecipientUser(companyId.Value, request);
            if (recipient == null) return BadRequest(new { message = "Recipient employee was not found." });
            recipientUsers = [recipient];
        }
        else if (recipientType.Equals("Multiple Employees", StringComparison.OrdinalIgnoreCase))
        {
            var requestedIds = (request.RecipientUserIds ?? []).Distinct().ToList();
            if (requestedIds.Count == 0 || requestedIds.Count > 250)
                return BadRequest(new { message = "Choose between 1 and 250 active employees." });

            recipientUsers = await ResolveMultipleRecipientUsers(companyId.Value, requestedIds);
            if (recipientUsers.Count != requestedIds.Count)
                return BadRequest(new { message = "One or more selected recipients are not active members of your company." });
        }
        else if (recipientType.Equals("Department", StringComparison.OrdinalIgnoreCase))
        {
            recipientDepartment = await ResolveRecipientDepartment(companyId.Value, request);
            if (recipientDepartment == null)
                return BadRequest(new { message = "Recipient department was not found." });

            recipientUsers = await _db.Users
                .Where(user => user.CompanyId == companyId.Value &&
                               user.DepartmentId == recipientDepartment.DepartmentId &&
                               user.IsActive && user.Email != null && user.Email != "")
                .OrderBy(user => user.UserId)
                .Take(251)
                .ToListAsync(cancellationToken);
        }
        else
        {
            recipientUsers = await _db.Users
                .Where(user => user.CompanyId == companyId.Value &&
                               user.IsActive && user.Email != null && user.Email != "")
                .OrderBy(user => user.UserId)
                .Take(251)
                .ToListAsync(cancellationToken);
        }

        if (recipientUsers.Count == 0)
            return BadRequest(new { message = "No active recipients with email addresses were found." });
        if (recipientUsers.Count > 250)
            return BadRequest(new { message = "A communication can include at most 250 recipients." });

        if (recipientUsers.Any(user =>
            !MailAddress.TryCreate(user.Email, out var address) ||
            !string.Equals(address.Address, user.Email, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "One or more recipient email addresses are invalid. Correct the user records before sending." });

        var now = DateTime.UtcNow;
        var batchId = Guid.NewGuid();
        var subject = request.Subject.Trim();
        var body = request.Body.Trim();

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var approval = new CommunicationApproval
        {
            BatchId = batchId,
            CompanyId = companyId.Value,
            RequestedByUserId = userId.Value,
            Status = "PendingApproval",
            RequestedAt = now
        };
        _db.CommunicationApprovals.Add(approval);

        var logs = recipientUsers.Select(recipient => new CommunicationLog
        {
            CompanyId = companyId.Value,
            SentBy = userId.Value,
            RecipientUserId = recipient.UserId,
            RecipientDepartmentId = recipientDepartment?.DepartmentId,
            CommunicationType = communicationType,
            Subject = subject,
            Body = body,
            SentAt = now
        }).ToList();
        _db.CommunicationLogs.AddRange(logs);

        _db.AIActionsLogs.Add(new AIActionsLog
        {
            ActionType = 5,
            UserId = userId.Value,
            InputData = JsonSerializer.Serialize(new
            {
                batchId,
                recipientType,
                recipientUserIds = recipientUsers.Select(recipient => recipient.UserId),
                recipientDepartmentId = recipientDepartment?.DepartmentId,
                request.RecommendationUsed,
                ccCount = ccEmails.Count
            }),
            OutputData = "Communication submitted for approval. No outbound email has been sent.",
            ModelVersion = "communication-approval-outbox",
            Timestamp = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        var ccJson = ccEmails.Count == 0 ? null : JsonSerializer.Serialize(ccEmails);
        var outboxItems = logs.Select(log =>
        {
            var recipient = recipientUsers.Single(user => user.UserId == log.RecipientUserId);
            return new CommunicationOutbox
            {
                CompanyId = companyId.Value,
                ApprovalId = approval.ApprovalId,
                CommunicationId = log.CommunicationId,
                RecipientUserId = recipient.UserId,
                RecipientEmail = recipient.Email,
                CcEmailsJson = ccJson,
                Subject = subject,
                Body = body,
                Status = "PendingApproval",
                AttemptCount = 0,
                CreatedAt = now,
                AvailableAt = now,
                IdempotencyKey = Guid.NewGuid()
            };
        }).ToList();

        _db.CommunicationOutbox.AddRange(outboxItems);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction != null)
            await transaction.CommitAsync(cancellationToken);

        return Accepted(new
        {
            approvalId = approval.ApprovalId,
            batchId,
            status = "PendingApproval",
            recipientCount = outboxItems.Count,
            message = "Your communication was submitted for approval. No email will be sent until a different CEO or Admin approves it."
        });
    }

    private async Task<User?> ResolveRecipientUser(int companyId, CreateCommunicationRequest request)
    {
        if (request.RecipientUserId.HasValue)
        {
            return await _db.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.CompanyId == companyId && u.UserId == request.RecipientUserId.Value && u.IsActive);
        }

        var recipient = request.Recipient?.Trim();
        if (string.IsNullOrWhiteSpace(recipient)) return null;

        return await _db.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u =>
                u.CompanyId == companyId && u.IsActive &&
                (u.Email == recipient || u.FullName == recipient || (u.FirstName + " " + u.LastName) == recipient));
    }

    private async Task<List<User>> ResolveMultipleRecipientUsers(int companyId, List<int>? recipientUserIds)
    {
        if (recipientUserIds == null || recipientUserIds.Count == 0)
        {
            return [];
        }

        return await _db.Users
            .Include(u => u.Department)
            .Where(u => u.CompanyId == companyId && recipientUserIds.Contains(u.UserId) && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }

    private async Task<Department?> ResolveRecipientDepartment(int companyId, CreateCommunicationRequest request)
    {
        if (request.RecipientDepartmentId.HasValue)
        {
            return await _db.Departments.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.DepartmentId == request.RecipientDepartmentId.Value);
        }

        var recipient = request.Recipient?.Trim();
        if (string.IsNullOrWhiteSpace(recipient)) return null;

        return await _db.Departments.FirstOrDefaultAsync(d => d.CompanyId == companyId && d.DepartmentName == recipient);
    }

    private async Task<List<User>> ResolveRecipientsAsync(int companyId, string? recipientType, int? recipientUserId, List<int>? recipientUserIds, int? recipientDepartmentId)
    {
        var type = recipientType?.Trim() ?? "Individual";
        if (type.Equals("Individual", StringComparison.OrdinalIgnoreCase))
        {
            var request = new CreateCommunicationRequest { RecipientUserId = recipientUserId };
            var user = await ResolveRecipientUser(companyId, request);
            return user == null ? [] : [user];
        }

        if (type.Equals("Multiple Employees", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveMultipleRecipientUsers(companyId, recipientUserIds);
        }

        if (type.Equals("Department", StringComparison.OrdinalIgnoreCase) && recipientDepartmentId.HasValue)
        {
            return await _db.Users
                .Include(u => u.Department)
                .Where(u => u.CompanyId == companyId && u.DepartmentId == recipientDepartmentId.Value && u.IsActive)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();
        }

        return await _db.Users
            .Include(u => u.Department)
            .Where(u => u.CompanyId == companyId && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }

    private static string GetDeliveryStatus(string? status) => status switch
    {
        "PendingApproval" => "PendingApproval",
        "Queued" => "Queued",
        "Processing" => "Sending",
        "Sent" => "Sent",
        "Failed" => "Failed",
        "Rejected" => "Rejected",
        _ => "Recorded"
    };

    private static byte MapCommunicationType(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "warning" => 2,
        "announcement" => 3,
        _ => 1
    };

    private static string GetCommunicationTypeName(byte communicationType) => communicationType switch
    {
        2 => "Warning",
        3 => "Announcement",
        _ => "Email"
    };

    private static List<CommunicationTemplateDto> GetTemplates() =>
    [
        new("performance_review", "Performance Review Notice", "Performance review follow-up", "Hello,\n\nThis is a reminder regarding your upcoming performance review.\n\nBest regards,\nHR Team"),
        new("attendance_warning", "Attendance Warning", "Attendance warning", "Hello,\n\nWe need to discuss your recent attendance record.\n\nRegards,\nHR Team"),
        new("team_meeting", "Team Meeting Invitation", "Team meeting invitation", "Hello team,\n\nYou are invited to attend the upcoming team meeting.\n\nBest regards,"),
        new("company_announcement", "Company Announcement", "Company announcement", "Hello everyone,\n\nPlease review the following company update.\n\nThanks,")
    ];
}

public class CreateCommunicationRequest
{
    public List<int>? RecipientUserIds { get; set; }
    public int? RecipientUserId { get; set; }
    public int? RecipientDepartmentId { get; set; }
    public string? RecipientType { get; set; }
    public string? Recipient { get; set; }
    public string? Type { get; set; }
    public byte? CommunicationType { get; set; }
    public List<string>? CcEmails { get; set; }
    public bool RecommendationUsed { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class CommunicationRecommendationRequest
{
    public List<int>? RecipientUserIds { get; set; }
    public int? RecipientUserId { get; set; }
    public int? RecipientDepartmentId { get; set; }
    public string? RecipientType { get; set; }
    public string? Recipient { get; set; }
    public string? Type { get; set; }
    public string? CommunicationType { get; set; }
    public string? TemplateKey { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? Prompt { get; set; }
}

public record CommunicationTemplateDto(string key, string label, string subject, string body);

public record CommunicationReviewRequest(string? Note);
