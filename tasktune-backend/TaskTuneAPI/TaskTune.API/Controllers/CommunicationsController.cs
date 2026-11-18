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
    private readonly CommunicationApprovalService _approvalService;
    private readonly CommunicationOutboxOperationsService _outboxOperations;
    private readonly CommunicationOutboxMetricsService _outboxMetrics;
    private readonly TenantUsageMeterService _usage;

    public CommunicationsController(
        TaskTuneDbContext db,
        IConfiguration configuration,
        CommunicationApprovalService approvalService,
        CommunicationOutboxOperationsService outboxOperations,
        CommunicationOutboxMetricsService outboxMetrics,
        TenantUsageMeterService usage)
    {
        _db = db;
        _configuration = configuration;
        _approvalService = approvalService;
        _outboxOperations = outboxOperations;
        _outboxMetrics = outboxMetrics;
        _usage = usage;
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
                c.SentAt
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
                status = "Recorded",
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

        var recentCount = await _db.CommunicationLogs.CountAsync(c => c.CompanyId == companyId.Value);
        var lastSentAt = await _db.CommunicationLogs
            .Where(c => c.CompanyId == companyId.Value)
            .OrderByDescending(c => c.SentAt)
            .Select(c => (DateTime?)c.SentAt)
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
                scheduled = 0,
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

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
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
            AddReportedAiTokenUsage(result, "email_assistant");
            await _db.SaveChangesAsync();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Failed to reach AI email agent." });
        }
    }

    private void AddReportedAiTokenUsage(JsonElement result, string source)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("token_usage", out var usage) ||
            usage.ValueKind != JsonValueKind.Object ||
            !usage.TryGetProperty("total_tokens", out var total) ||
            !total.TryGetInt64(out var totalTokens) ||
            totalTokens <= 0)
            return;

        _usage.AddAiTokenUsageEvent(
            totalTokens,
            $"email-ai-{Guid.NewGuid():N}",
            source,
            DateTime.UtcNow);
    }

    [HttpGet("approvals")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> GetApprovals(
        [FromQuery] string status = "Pending",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        var normalizedStatus = status.Trim();
        if (!new[] { "Pending", "Approved", "Rejected" }.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Status must be Pending, Approved, or Rejected." });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var currentUserId = GetCurrentUserId();
        var query = _db.CommunicationApprovals.AsNoTracking()
            .Include(a => a.RequestedByUser)
            .Where(a => a.CompanyId == companyId.Value && a.Status == normalizedStatus);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.ApprovalId, a.Subject, a.Body, a.CommunicationType, a.RecipientType,
                a.RecipientEmailsJson, a.CcEmailsJson, a.Status, a.CreatedAt,
                a.RequestedByUserId, RequesterName = a.RequestedByUser.FullName
            }).ToListAsync();

        var items = rows.Select(a =>
        {
            var recipients = JsonSerializer.Deserialize<List<string>>(a.RecipientEmailsJson) ?? [];
            var cc = JsonSerializer.Deserialize<List<string>>(a.CcEmailsJson) ?? [];
            return new
            {
                a.ApprovalId, a.Subject, a.Body,
                type = GetCommunicationTypeName(a.CommunicationType),
                a.RecipientType, recipientCount = recipients.Count, recipients, cc,
                a.Status, a.CreatedAt, a.RequestedByUserId,
                requesterName = a.RequesterName,
                canReview = a.RequestedByUserId != currentUserId
            };
        });
        return Ok(new { items, totalCount = total });
    }

    [HttpPost]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateCommunication([FromBody] CreateCommunicationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null) return Unauthorized();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });
        if (request == null || string.IsNullOrWhiteSpace(request.Subject) ||
            request.Subject.Trim().Length > 200 || string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 20000)
            return BadRequest(new { message = "Subject (1–200 characters) and body (1–20000 characters) are required." });
        if (request.IdempotencyKey == Guid.Empty)
            return BadRequest(new { message = "A valid idempotency key is required. Retry the same request with the same key." });

        var existing = await _db.CommunicationApprovals.AsNoTracking()
            .Where(a => a.CompanyId == companyId.Value && a.IdempotencyKey == request.IdempotencyKey)
            .Select(a => new { a.ApprovalId, a.Status, a.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
            return Accepted(new
            {
                approvalId = existing.ApprovalId, status = existing.Status, createdAt = existing.CreatedAt,
                duplicate = true, message = "This request was already recorded. No additional message was queued."
            });

        var recipientType = request.RecipientType?.Trim() ?? "Individual";
        if (!new[] { "Individual", "Multiple Employees", "Department", "Company" }
            .Contains(recipientType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid recipient type." });

        var communicationType = request.CommunicationType ?? MapCommunicationType(request.Type);
        if (communicationType is < 1 or > 3)
            return BadRequest(new { message = "Invalid communication type." });

        var ccEmails = (request.CcEmails ?? []).Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ccEmails.Count > 25 || ccEmails.Any(email =>
            !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "CC addresses must be valid and cannot exceed 25 recipients." });

        if (ccEmails.Count > 0)
        {
            var allowedCc = await _db.Users
                .Where(u => u.CompanyId == companyId.Value && u.IsActive && u.Email != null && ccEmails.Contains(u.Email))
                .Select(u => u.Email!).ToListAsync(cancellationToken);
            if (allowedCc.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ccEmails.Count)
                return BadRequest(new { message = "CC recipients must be active users in your company." });
        }

        List<User> recipientUsers = [];
        if (recipientType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
        {
            var recipientUser = await ResolveRecipientUser(companyId.Value, request);
            if (recipientUser == null) return BadRequest(new { message = "Recipient employee was not found in this company." });
            recipientUsers.Add(recipientUser);
        }
        else if (recipientType.Equals("Multiple Employees", StringComparison.OrdinalIgnoreCase))
        {
            var requestedIds = (request.RecipientUserIds ?? []).Distinct().ToArray();
            if (requestedIds.Length == 0 || requestedIds.Length != (request.RecipientUserIds?.Count ?? 0))
                return BadRequest(new { message = "Choose one or more unique employees." });
            recipientUsers = await ResolveMultipleRecipientUsers(companyId.Value, requestedIds.ToList());
            if (recipientUsers.Count != requestedIds.Length)
                return BadRequest(new { message = "One or more selected recipients are unavailable in this company." });
        }
        else if (recipientType.Equals("Department", StringComparison.OrdinalIgnoreCase))
        {
            var department = await ResolveRecipientDepartment(companyId.Value, request);
            if (department == null) return BadRequest(new { message = "Recipient department was not found in this company." });
            recipientUsers = await _db.Users
                .Where(u => u.CompanyId == companyId.Value && u.DepartmentId == department.DepartmentId && u.IsActive)
                .OrderBy(u => u.UserId).ToListAsync(cancellationToken);
        }
        else
        {
            recipientUsers = await _db.Users.Where(u => u.CompanyId == companyId.Value && u.IsActive)
                .OrderBy(u => u.UserId).ToListAsync(cancellationToken);
        }

        if (recipientUsers.Count == 0)
            return BadRequest(new { message = "The selected audience has no active recipients." });
        if (recipientUsers.Count > 1000)
            return BadRequest(new { message = "A communication request cannot exceed 1,000 recipients." });
        if (recipientUsers.Any(u => string.IsNullOrWhiteSpace(u.Email) || !MailAddress.TryCreate(u.Email, out _)))
            return BadRequest(new { message = "Every selected recipient must have a valid email address before submission." });

        var recipientEmails = recipientUsers.Select(u => u.Email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var approval = new CommunicationApproval
        {
            CompanyId = companyId.Value,
            RequestedByUserId = userId.Value,
            CommunicationType = communicationType,
            RecipientType = recipientType,
            RecipientEmailsJson = JsonSerializer.Serialize(recipientEmails),
            CcEmailsJson = JsonSerializer.Serialize(ccEmails),
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            Status = "Pending",
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        _db.CommunicationApprovals.Add(approval);
        _db.AIActionsLogs.Add(new AIActionsLog
        {
            ActionType = 5, UserId = userId.Value,
            InputData = JsonSerializer.Serialize(new
            {
                recipientType, recipientCount = recipientEmails.Count,
                ccCount = ccEmails.Count, request.RecommendationUsed
            }),
            OutputData = "Communication approval requested; no external delivery was attempted.",
            ModelVersion = "communication_approval", Timestamp = DateTime.UtcNow
        });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique (CompanyId, IdempotencyKey) index is the final race-safe guard.
            // If another request won the insert race, return its durable result instead of 500.
            _db.ChangeTracker.Clear();
            var duplicate = await _db.CommunicationApprovals.AsNoTracking()
                .Where(a => a.CompanyId == companyId.Value && a.IdempotencyKey == request.IdempotencyKey)
                .Select(a => new { a.ApprovalId, a.Status, a.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);
            if (duplicate == null) throw;

            return Accepted(new
            {
                approvalId = duplicate.ApprovalId,
                status = duplicate.Status,
                createdAt = duplicate.CreatedAt,
                duplicate = true,
                message = "This request was already recorded. No additional message was queued."
            });
        }

        await AuditLogWriter.WriteAsync(_db, HttpContext, userId.Value,
            "communication.requested", "CommunicationApproval", approval.ApprovalId,
            after: new { status = approval.Status, recipientCount = recipientEmails.Count });

        return Accepted(new
        {
            approvalId = approval.ApprovalId, status = approval.Status,
            recipientCount = recipientEmails.Count, createdAt = approval.CreatedAt,
            message = "Communication submitted for approval. No email has been sent."
        });
    }

    [HttpPost("approvals/{approvalId:int}/approve")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> ApproveCommunication(int approvalId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();

        var result = await _approvalService.ApproveAsync(approvalId, companyId.Value, userId.Value, cancellationToken);
        if (result == CommunicationApprovalResult.NotFound) return NotFound();
        if (result == CommunicationApprovalResult.CannotReviewOwnRequest)
            return Conflict(new { message = "The requester cannot approve their own communication." });
        if (result == CommunicationApprovalResult.AlreadyReviewed)
            return Conflict(new { message = "This communication has already been reviewed." });
        if (result == CommunicationApprovalResult.RecipientQuotaExceeded)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "This communication exceeds the configured per-request recipient quota." });
        if (result == CommunicationApprovalResult.TenantQueueQuotaExceeded)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "The company pending delivery quota is full. Wait for queued messages to be processed before approving more." });
        if (result != CommunicationApprovalResult.Success)
            return Conflict(new { message = "The approval could not be completed. Refresh the queue and try again." });

        await AuditLogWriter.WriteAsync(_db, HttpContext, userId.Value,
            "communication.approved", "CommunicationApproval", approvalId,
            after: new { status = "Approved", deliveryStatus = "Pending" });
        return Ok(new
        {
            approvalId, status = "Approved", deliveryStatus = "Pending",
            message = "Approved and added to the durable delivery outbox. Delivery remains disabled until the outbox worker is enabled."
        });
    }

    [HttpPost("approvals/{approvalId:int}/reject")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> RejectCommunication(int approvalId, [FromBody] RejectCommunicationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        var reason = request?.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5 || reason.Length > 1000)
            return BadRequest(new { message = "A rejection reason between 5 and 1,000 characters is required." });

        var result = await _approvalService.RejectAsync(approvalId, companyId.Value, userId.Value, reason, cancellationToken);
        if (result == CommunicationApprovalResult.NotFound) return NotFound();
        if (result == CommunicationApprovalResult.CannotReviewOwnRequest)
            return Conflict(new { message = "The requester cannot reject their own communication." });
        if (result == CommunicationApprovalResult.AlreadyReviewed)
            return Conflict(new { message = "This communication has already been reviewed." });
        if (result != CommunicationApprovalResult.Success)
            return Conflict(new { message = "The communication could not be rejected. Refresh the queue and try again." });

        await AuditLogWriter.WriteAsync(_db, HttpContext, userId.Value,
            "communication.rejected", "CommunicationApproval", approvalId,
            after: new { status = "Rejected", reason });
        return Ok(new { approvalId, status = "Rejected", message = "Communication rejected. No email was sent." });
    }

    [HttpGet("outbox/metrics")]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> GetOutboxMetrics(CancellationToken cancellationToken)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        var workerEnabled = _configuration.GetValue<bool>("Email:Outbox:Enabled");
        var warningMinutes = Math.Clamp(_configuration.GetValue<int?>("Email:Outbox:QueueAgeWarningMinutes") ?? 15, 1, 1440);
        var metrics = await _outboxMetrics.GetAsync(companyId.Value, warningMinutes, workerEnabled, cancellationToken);
        return Ok(metrics);
    }

    [HttpGet("outbox/dead-letter")]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> GetDeadLetterOutbox(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.CommunicationOutbox.AsNoTracking()
            .Where(item => item.CompanyId == companyId.Value && item.Status == "DeadLetter");

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.OutboxId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                outboxId = item.OutboxId,
                item.ApprovalId,
                item.RecipientEmail,
                item.Subject,
                item.Attempts,
                item.CreatedAt,
                item.LastError
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items,
            totalCount = total,
            deliveryWorkerEnabled = _configuration.GetValue<bool>("Email:Outbox:Enabled")
        });
    }

    [HttpPost("outbox/{outboxId:long}/replay")]
    [Authorize(Roles = "CEO,Admin")]
    public async Task<IActionResult> ReplayDeadLetter(
        long outboxId,
        [FromBody] ReplayDeadLetterRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = GetCurrentCompanyId();
        var userId = GetCurrentUserId();
        if (companyId == null || userId == null) return Unauthorized();

        var reason = request?.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 5 || reason.Length > 1000)
            return BadRequest(new { message = "A reconciliation reason between 5 and 1,000 characters is required." });
        if (request?.ProviderConfirmedNotAccepted != true)
            return BadRequest(new { message = "Replay is allowed only after provider logs confirm the original message was not accepted." });

        var result = await _outboxOperations.ReplayDeadLetterAsync(
            outboxId, companyId.Value, userId.Value, reason,
            request.ProviderConfirmedNotAccepted, HttpContext, cancellationToken);

        if (result == CommunicationOutboxReplayResult.NotFound) return NotFound();
        if (result == CommunicationOutboxReplayResult.NotDeadLetter)
            return Conflict(new { message = "Only dead-lettered deliveries can be replayed." });
        if (result == CommunicationOutboxReplayResult.InvalidApprovalState)
            return Conflict(new { message = "The parent approval is no longer approved. The delivery was not replayed." });
        if (result == CommunicationOutboxReplayResult.ProviderConfirmationRequired)
            return BadRequest(new { message = "Provider non-acceptance confirmation is required before replay." });
        if (result == CommunicationOutboxReplayResult.Conflict)
            return Conflict(new { message = "The delivery changed while replay was being requested. Refresh the queue and try again." });

        return Accepted(new
        {
            outboxId,
            status = "Pending",
            attemptsReset = true,
            message = "Delivery returned to the queue. SMTP is at-least-once: this may duplicate a message if the previous attempt was accepted by the provider but not recorded. Verify provider logs before replay."
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

    private static System.Threading.Tasks.Task<(bool success, string? error)> TrySendEmailsAsync(
        IEnumerable<string> recipients,
        IEnumerable<string>? ccEmails,
        string subject,
        string body)
    {
        // Direct SMTP sending is intentionally disabled until communication approval,
        // delivery idempotency and an auditable recipient allowlist are implemented.
        // The request is recorded for review; this endpoint does not send external mail.
        return System.Threading.Tasks.Task.FromResult<(bool success, string? error)>((
            false,
            "Pending approval: direct outbound email is disabled until the approval workflow is available."));
    }

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
    public Guid IdempotencyKey { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed class RejectCommunicationRequest
{
    public string? Reason { get; set; }
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


public sealed class ReplayDeadLetterRequest
{
    public string? Reason { get; set; }
    public bool ProviderConfirmedNotAccepted { get; set; }
}
