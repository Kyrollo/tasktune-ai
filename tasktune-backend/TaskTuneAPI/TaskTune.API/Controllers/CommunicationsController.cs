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

    public CommunicationsController(TaskTuneDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
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
        var aiConfig = await AIConfigurationStore.LoadAsync(_db, companyId.Value);
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
        if (!AgentServiceSecurity.TryConfigure(http, _configuration))
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

    [HttpPost]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateCommunication([FromBody] CreateCommunicationRequest request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null) return Unauthorized();
        if (companyId == null) return BadRequest(new { message = "Company ID not found in token." });
        if (request == null || string.IsNullOrWhiteSpace(request.Subject) ||
            request.Subject.Length > 200 || string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 20000)
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
                .Where(u => u.CompanyId == companyId.Value && u.IsActive && u.Email != null && ccEmails.Contains(u.Email))
                .Select(u => u.Email!)
                .ToListAsync();
            if (companyCcEmails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ccEmails.Count)
                return BadRequest(new { message = "CC recipients must be active users in your company." });
        }

        List<User> recipientUsers = [];
        Department? recipientDepartment = null;
        List<string> recipientEmails = [];

        if (recipientType.Equals("Individual", StringComparison.OrdinalIgnoreCase))
        {
            var recipientUser = await ResolveRecipientUser(companyId.Value, request);
            if (recipientUser == null) return BadRequest(new { message = "Recipient employee was not found." });
            recipientUsers.Add(recipientUser);
            if (!string.IsNullOrWhiteSpace(recipientUser.Email)) recipientEmails.Add(recipientUser.Email);
        }
        else if (recipientType.Equals("Multiple Employees", StringComparison.OrdinalIgnoreCase))
        {
            recipientUsers = await ResolveMultipleRecipientUsers(companyId.Value, request.RecipientUserIds);
            if (recipientUsers.Count == 0) return BadRequest(new { message = "Please choose at least one employee." });
            recipientEmails = recipientUsers
                .Select(u => u.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }
        else if (recipientType.Equals("Department", StringComparison.OrdinalIgnoreCase))
        {
            recipientDepartment = await ResolveRecipientDepartment(companyId.Value, request);
            if (recipientDepartment == null) return BadRequest(new { message = "Recipient department was not found." });
            recipientEmails = await _db.Users
                .Where(u => u.CompanyId == companyId.Value && u.DepartmentId == recipientDepartment.DepartmentId && u.IsActive)
                .Select(u => u.Email)
                .Where(e => e != null && e != "")
                .ToListAsync();
        }
        else
        {
            recipientEmails = await _db.Users
                .Where(u => u.CompanyId == companyId.Value && u.IsActive)
                .Select(u => u.Email)
                .Where(e => e != null && e != "")
                .ToListAsync();
        }

        var emailResult = await TrySendEmailsAsync(recipientEmails, request.CcEmails, request.Subject, request.Body);

        var logs = new List<CommunicationLog>();
        if (recipientUsers.Count > 0)
        {
            logs.AddRange(recipientUsers.Select(recipientUser => new CommunicationLog
            {
                CompanyId = companyId.Value,
                SentBy = userId.Value,
                RecipientUserId = recipientUser.UserId,
                RecipientDepartmentId = null,
                CommunicationType = communicationType,
                Subject = request.Subject.Trim(),
                Body = request.Body.Trim(),
                SentAt = DateTime.UtcNow
            }));
        }
        else
        {
            logs.Add(new CommunicationLog
            {
                CompanyId = companyId.Value,
                SentBy = userId.Value,
                RecipientUserId = null,
                RecipientDepartmentId = recipientDepartment?.DepartmentId,
                CommunicationType = communicationType,
                Subject = request.Subject.Trim(),
                Body = request.Body.Trim(),
                SentAt = DateTime.UtcNow
            });
        }

        _db.CommunicationLogs.AddRange(logs);
        _db.AIActionsLogs.Add(new AIActionsLog
        {
            ActionType = 5,
            UserId = userId.Value,
            InputData = JsonSerializer.Serialize(new
            {
                recipientType,
                recipientUserIds = request.RecipientUserIds,
                request.RecipientDepartmentId,
                request.Recipient,
                request.CcEmails,
                request.RecommendationUsed
            }),
            OutputData = emailResult.success ? "Email processed successfully." : $"Email saved, SMTP skipped: {emailResult.error}",
            ModelVersion = "agent5_email",
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var primaryLog = logs.First();
        return StatusCode(201, new
        {
            primaryLog.CommunicationId,
            id = primaryLog.CommunicationId,
            type = GetCommunicationTypeName(primaryLog.CommunicationType),
            status = emailResult.success ? "Sent" : "PendingApproval",
            recipientType,
            recipient = recipientUsers.Count > 0
                ? string.Join(", ", recipientUsers.Select(u => u.FullName ?? (u.FirstName + " " + u.LastName)))
                : recipientDepartment?.DepartmentName ?? "All Employees",
            subject = primaryLog.Subject,
            message = primaryLog.Body,
            sentAt = primaryLog.SentAt,
            count = logs.Count,
            emailResult.success,
            emailResult.error
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
