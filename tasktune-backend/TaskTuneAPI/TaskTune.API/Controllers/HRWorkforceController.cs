using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/hr-workforce")]
[RequireModule("core_hr")]
[Authorize]
public sealed class HRWorkforceController : BaseApiController
{
    private readonly TaskTuneDbContext _db;
    private static readonly Regex CurrencyPattern = new("^[A-Za-z]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public HRWorkforceController(TaskTuneDbContext db) => _db = db;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var scope = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        var ids = scope.ToArray();
        var isHr = IsHrAdmin();
        var applicantCount = isHr ? await _db.JobApplicants.CountAsync(x => x.CompanyId == companyId.Value && x.Stage != "rejected" && x.Stage != "hired") : 0;
        var onboardingOpen = await _db.OnboardingTasks.CountAsync(x => x.CompanyId == companyId.Value && ids.Contains(x.EmployeeUserId) && x.Status == "open");
        var pendingExpenses = await _db.ExpenseClaims.CountAsync(x => x.CompanyId == companyId.Value && ids.Contains(x.UserId) && x.Status == "pending");
        var activeGoals = await _db.PerformanceGoals.CountAsync(x => x.CompanyId == companyId.Value && ids.Contains(x.UserId) && x.Status == "active");
        var assignedAssets = await _db.CompanyAssets.CountAsync(x => x.CompanyId == companyId.Value && x.AssignedToUserId == actorId.Value && x.Status == "assigned");
        return Ok(new { applicantsInPipeline = applicantCount, openOnboardingTasks = onboardingOpen, pendingExpenseClaims = pendingExpenses, activeGoals, myAssignedAssets = assignedAssets });
    }

    // Recruitment
    [HttpGet("applicants")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> GetApplicants([FromQuery] string? stage = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.JobApplicants.AsNoTracking().Where(x => x.CompanyId == companyId.Value);
        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(x => x.Stage == stage.Trim().ToLowerInvariant());
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.JobApplicantId, x.CandidateName, x.Email, x.Phone, x.JobTitle, x.Source, x.Stage, x.Notes, x.CreatedAt, x.UpdatedAt })
            .ToListAsync();
        return Ok(new { items, totalCount = total, page, pageSize });
    }

    [HttpPost("applicants")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateApplicant([FromBody] CreateApplicantRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.CandidateName) || string.IsNullOrWhiteSpace(request.JobTitle))
            return BadRequest(new { message = "Candidate name and job title are required." });
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email.Length > 254)
            return BadRequest(new { message = "Email must not exceed 254 characters." });
        var now = DateTime.UtcNow;
        var item = new JobApplicant { CompanyId = companyId.Value, CandidateName = request.CandidateName.Trim(), Email = request.Email?.Trim() ?? "", Phone = request.Phone?.Trim() ?? "", JobTitle = request.JobTitle.Trim(), Source = string.IsNullOrWhiteSpace(request.Source) ? "direct" : request.Source.Trim(), Stage = "applied", Notes = request.Notes?.Trim() ?? "", CreatedBy = actorId.Value, CreatedAt = now, UpdatedAt = now };
        _db.JobApplicants.Add(item); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "JobApplicant", item.JobApplicantId, after: new { item.CandidateName, item.JobTitle, item.Stage });
        return CreatedAtAction(nameof(GetApplicants), new { }, new { item.JobApplicantId, item.CandidateName, item.JobTitle, item.Stage });
    }

    [HttpPut("applicants/{id:int}/stage")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> UpdateApplicantStage(int id, [FromBody] UpdateApplicantStageRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var item = await _db.JobApplicants.FirstOrDefaultAsync(x => x.JobApplicantId == id && x.CompanyId == companyId.Value);
        if (item == null) return NotFound();
        var next = request.Stage?.Trim().ToLowerInvariant() ?? "";
        if (!HRWorkflowPolicy.IsValidApplicantTransition(item.Stage, next))
            return Conflict(new { message = $"Invalid recruitment stage transition: {item.Stage} → {next}." });
        var before = item.Stage; item.Stage = next; item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "STAGE", "JobApplicant", id, before: new { stage = before }, after: new { stage = item.Stage });
        return Ok(new { item.JobApplicantId, item.Stage, item.UpdatedAt });
    }

    // Onboarding
    [HttpGet("onboarding")]
    public async Task<IActionResult> GetOnboarding([FromQuery] int? employeeUserId = null)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var scope = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (employeeUserId.HasValue && !scope.Contains(employeeUserId.Value)) return NotFound();
        var ids = employeeUserId.HasValue ? new[] { employeeUserId.Value } : scope.ToArray();
        var items = await _db.OnboardingTasks.AsNoTracking().Where(x => x.CompanyId == companyId.Value && ids.Contains(x.EmployeeUserId))
            .OrderBy(x => x.Status).ThenBy(x => x.DueDate)
            .Select(x => new { x.OnboardingTaskId, x.EmployeeUserId, x.Title, x.Description, x.DueDate, x.Status, x.CreatedAt, x.CompletedAt })
            .ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("onboarding")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateOnboardingTask([FromBody] CreateOnboardingTaskRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 180) return BadRequest(new { message = "A task title up to 180 characters is required." });
        if (!await _db.Users.AnyAsync(x => x.CompanyId == companyId.Value && x.UserId == request.EmployeeUserId && x.IsActive))
            return NotFound(new { message = "Active employee was not found in this company." });
        var item = new OnboardingTask { CompanyId = companyId.Value, EmployeeUserId = request.EmployeeUserId, Title = request.Title.Trim(), Description = request.Description?.Trim() ?? "", DueDate = request.DueDate, Status = "open", CreatedBy = actorId.Value, CreatedAt = DateTime.UtcNow };
        _db.OnboardingTasks.Add(item); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "OnboardingTask", item.OnboardingTaskId, after: new { item.EmployeeUserId, item.Title, item.DueDate });
        return StatusCode(201, new { item.OnboardingTaskId, item.EmployeeUserId, item.Title, item.DueDate, item.Status });
    }

    [HttpPut("onboarding/{id:int}/complete")]
    public async Task<IActionResult> CompleteOnboardingTask(int id)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var item = await _db.OnboardingTasks.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.OnboardingTaskId == id);
        if (item == null) return NotFound();
        var visibleIds = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (!visibleIds.Contains(item.EmployeeUserId)) return NotFound();
        if (item.Status == "completed") return Conflict(new { message = "This onboarding task is already completed." });
        item.Status = "completed"; item.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "COMPLETE", "OnboardingTask", id, after: new { item.Status, item.CompletedAt });
        return Ok(new { item.OnboardingTaskId, item.Status, item.CompletedAt });
    }

    // Performance goals
    [HttpGet("goals")]
    public async Task<IActionResult> GetGoals([FromQuery] int? employeeUserId = null)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var scope = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (employeeUserId.HasValue && !scope.Contains(employeeUserId.Value)) return NotFound();
        var ids = employeeUserId.HasValue ? new[] { employeeUserId.Value } : scope.ToArray();
        var items = await _db.PerformanceGoals.AsNoTracking().Where(x => x.CompanyId == companyId.Value && ids.Contains(x.UserId))
            .OrderByDescending(x => x.EndDate)
            .Select(x => new { x.PerformanceGoalId, x.UserId, x.Title, x.Description, x.MetricName, x.TargetValue, x.CurrentValue, x.StartDate, x.EndDate, x.Status })
            .ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("goals")]
    public async Task<IActionResult> CreateGoal([FromBody] CreateGoalRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var targetUserId = request.UserId ?? actorId.Value;
        var visibleIds = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (!visibleIds.Contains(targetUserId)) return NotFound();
        if (!HRWorkflowPolicy.IsValidGoalProgress(0m, request.TargetValue) || !HRWorkflowPolicy.IsValidGoalPeriod(request.StartDate, request.EndDate))
            return BadRequest(new { message = "Goal target must be positive and the end date cannot precede the start date." });
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(new { message = "Goal title is required." });
        var item = new PerformanceGoal { CompanyId = companyId.Value, UserId = targetUserId, Title = request.Title.Trim(), Description = request.Description?.Trim() ?? "", MetricName = request.MetricName?.Trim() ?? "", TargetValue = request.TargetValue, CurrentValue = 0, StartDate = request.StartDate, EndDate = request.EndDate, Status = "active", CreatedBy = actorId.Value, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.PerformanceGoals.Add(item); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "PerformanceGoal", item.PerformanceGoalId, after: new { item.UserId, item.Title, item.TargetValue });
        return StatusCode(201, new { item.PerformanceGoalId, item.UserId, item.Title, item.TargetValue, item.CurrentValue, item.Status });
    }

    [HttpPut("goals/{id:int}/progress")]
    public async Task<IActionResult> UpdateGoalProgress(int id, [FromBody] UpdateGoalProgressRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var item = await _db.PerformanceGoals.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.PerformanceGoalId == id);
        if (item == null) return NotFound();
        var visibleIds = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (!visibleIds.Contains(item.UserId)) return NotFound();
        if (!HRWorkflowPolicy.IsValidGoalProgress(request.CurrentValue, item.TargetValue)) return BadRequest(new { message = "Progress must be non-negative and goal target must be positive." });
        var before = item.CurrentValue; item.CurrentValue = request.CurrentValue;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            if (status is not ("active" or "paused" or "completed")) return BadRequest(new { message = "Status must be active, paused, or completed." });
            item.Status = status;
        }
        else if (item.CurrentValue >= item.TargetValue) item.Status = "completed";
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "PROGRESS", "PerformanceGoal", id, before: new { currentValue = before }, after: new { item.CurrentValue, item.Status });
        return Ok(new { item.PerformanceGoalId, item.CurrentValue, item.TargetValue, item.Status, item.UpdatedAt });
    }

    // Expense claims
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses([FromQuery] int? employeeUserId = null)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var scope = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (employeeUserId.HasValue && !scope.Contains(employeeUserId.Value)) return NotFound();
        var ids = employeeUserId.HasValue ? new[] { employeeUserId.Value } : scope.ToArray();
        var items = await _db.ExpenseClaims.AsNoTracking().Where(x => x.CompanyId == companyId.Value && ids.Contains(x.UserId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.ExpenseClaimId, x.UserId, x.Category, x.Description, x.Amount, x.CurrencyCode, x.ExpenseDate, x.ReceiptReference, x.Status, x.ReviewComment, x.ReviewedBy, x.ReviewedAt, x.CreatedAt })
            .ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var currency = (request.CurrencyCode ?? "").Trim().ToUpperInvariant();
        if (!HRWorkflowPolicy.IsValidExpense(request.Amount, currency) || !CurrencyPattern.IsMatch(currency))
            return BadRequest(new { message = "Expense amount must be positive and currency must be a three-letter ISO-style code." });
        if (string.IsNullOrWhiteSpace(request.Category)) return BadRequest(new { message = "Expense category is required." });
        var item = new ExpenseClaim { CompanyId = companyId.Value, UserId = actorId.Value, Category = request.Category.Trim(), Description = request.Description?.Trim() ?? "", Amount = request.Amount, CurrencyCode = currency, ExpenseDate = request.ExpenseDate, ReceiptReference = request.ReceiptReference?.Trim(), Status = "pending", CreatedAt = DateTime.UtcNow };
        _db.ExpenseClaims.Add(item); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "ExpenseClaim", item.ExpenseClaimId, after: new { item.Amount, item.CurrencyCode, item.Category });
        return StatusCode(201, new { item.ExpenseClaimId, item.Amount, item.CurrencyCode, item.Status });
    }

    [HttpPost("expenses/{id:int}/decision")]
    public async Task<IActionResult> DecideExpense(int id, [FromBody] DecideExpenseRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var decision = request.Decision?.Trim().ToLowerInvariant() ?? "";
        if (!HRWorkflowPolicy.IsValidDecision(decision)) return BadRequest(new { message = "Decision must be approved or rejected." });
        var item = await _db.ExpenseClaims.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.ExpenseClaimId == id);
        if (item == null) return NotFound();
        var visibleIds = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (!visibleIds.Contains(item.UserId)) return NotFound();
        if (item.UserId == actorId.Value) return Forbid();
        if (item.Status != "pending") return Conflict(new { message = "Only pending expense claims can be reviewed." });
        item.Status = decision; item.ReviewComment = request.Comment?.Trim(); item.ReviewedBy = actorId.Value; item.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, decision.ToUpperInvariant(), "ExpenseClaim", id, after: new { item.Status, item.ReviewedBy, item.ReviewedAt });
        return Ok(new { item.ExpenseClaimId, item.Status, item.ReviewComment, item.ReviewedAt });
    }

    // Company assets
    [HttpGet("assets")]
    public async Task<IActionResult> GetAssets()
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var query = _db.CompanyAssets.AsNoTracking().Where(x => x.CompanyId == companyId.Value);
        if (!IsHrAdmin()) query = query.Where(x => x.AssignedToUserId == actorId.Value);
        var items = await query.OrderBy(x => x.Name).Select(x => new { x.CompanyAssetId, x.AssetTag, x.Name, x.Category, x.SerialNumber, x.Status, x.AssignedToUserId, x.AssignedAt, x.ReturnedAt, x.Notes }).ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("assets")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateAsset([FromBody] CreateAssetRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.AssetTag) || string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Asset tag and name are required." });
        var tag = request.AssetTag.Trim();
        if (await _db.CompanyAssets.AnyAsync(x => x.CompanyId == companyId.Value && x.AssetTag == tag)) return Conflict(new { message = "Asset tag already exists." });
        var item = new CompanyAsset { CompanyId = companyId.Value, AssetTag = tag, Name = request.Name.Trim(), Category = request.Category?.Trim() ?? "", SerialNumber = request.SerialNumber?.Trim(), Notes = request.Notes?.Trim(), Status = "available", CreatedAt = DateTime.UtcNow };
        _db.CompanyAssets.Add(item); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "CompanyAsset", item.CompanyAssetId, after: new { item.AssetTag, item.Name });
        return StatusCode(201, new { item.CompanyAssetId, item.AssetTag, item.Name, item.Status });
    }

    [HttpPost("assets/{id:int}/assign")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> AssignAsset(int id, [FromBody] AssignAssetRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (!await _db.Users.AnyAsync(x => x.CompanyId == companyId.Value && x.UserId == request.EmployeeUserId && x.IsActive))
            return NotFound(new { message = "Active employee was not found." });
        var item = await _db.CompanyAssets.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.CompanyAssetId == id);
        if (item == null) return NotFound();
        if (item.Status != "available") return Conflict(new { message = "Only available assets can be assigned." });
        item.Status = "assigned"; item.AssignedToUserId = request.EmployeeUserId; item.AssignedAt = DateTime.UtcNow; item.ReturnedAt = null;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "ASSIGN", "CompanyAsset", id, after: new { item.AssignedToUserId, item.Status });
        return Ok(new { item.CompanyAssetId, item.Status, item.AssignedToUserId, item.AssignedAt });
    }

    [HttpPost("assets/{id:int}/return")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> ReturnAsset(int id)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var item = await _db.CompanyAssets.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.CompanyAssetId == id);
        if (item == null) return NotFound();
        if (item.Status != "assigned") return Conflict(new { message = "This asset is not currently assigned." });
        item.Status = "available"; item.AssignedToUserId = null; item.ReturnedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "RETURN", "CompanyAsset", id, after: new { item.Status, item.ReturnedAt });
        return Ok(new { item.CompanyAssetId, item.Status, item.ReturnedAt });
    }

    // Benefits
    [HttpGet("benefits/plans")]
    public async Task<IActionResult> GetBenefitPlans()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        var items = await _db.BenefitPlans.AsNoTracking().Where(x => x.CompanyId == companyId.Value && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new { x.BenefitPlanId, x.Name, x.ProviderName, x.Description, x.EmployeeContribution, x.EmployerContribution, x.CurrencyCode, x.EffectiveFrom, x.EffectiveTo })
            .ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("benefits/plans")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateBenefitPlan([FromBody] CreateBenefitPlanRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var currency = (request.CurrencyCode ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(request.Name) || !CurrencyPattern.IsMatch(currency) || request.EmployeeContribution < 0 || request.EmployerContribution < 0)
            return BadRequest(new { message = "Plan name, three-letter currency and non-negative contributions are required." });
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom) return BadRequest(new { message = "Effective end cannot precede effective start." });
        var item = new BenefitPlan { CompanyId = companyId.Value, Name = request.Name.Trim(), ProviderName = request.ProviderName?.Trim() ?? "", Description = request.Description?.Trim() ?? "", EmployeeContribution = request.EmployeeContribution, EmployerContribution = request.EmployerContribution, CurrencyCode = currency, IsActive = true, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, CreatedAt = DateTime.UtcNow };
        _db.BenefitPlans.Add(item); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "BenefitPlan", item.BenefitPlanId, after: new { item.Name, item.CurrencyCode });
        return StatusCode(201, new { item.BenefitPlanId, item.Name, item.CurrencyCode });
    }

    [HttpGet("benefits/enrollments")]
    public async Task<IActionResult> GetBenefitEnrollments([FromQuery] int? employeeUserId = null)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var scope = await GetVisibleEmployeeIdsAsync(companyId.Value, actorId.Value);
        if (employeeUserId.HasValue && !scope.Contains(employeeUserId.Value)) return NotFound();
        var ids = employeeUserId.HasValue ? new[] { employeeUserId.Value } : scope.ToArray();
        var items = await (
            from enrollment in _db.BenefitEnrollments.AsNoTracking()
            join plan in _db.BenefitPlans.AsNoTracking() on enrollment.BenefitPlanId equals plan.BenefitPlanId
            where enrollment.CompanyId == companyId.Value && enrollment.CompanyId == plan.CompanyId && ids.Contains(enrollment.UserId)
            orderby enrollment.CreatedAt descending
            select new { enrollment.BenefitEnrollmentId, enrollment.UserId, enrollment.BenefitPlanId, PlanName = plan.Name, plan.ProviderName, plan.EmployeeContribution, plan.EmployerContribution, plan.CurrencyCode, enrollment.Status, enrollment.Notes, enrollment.CreatedAt, enrollment.ReviewedAt })
            .ToListAsync();
        return Ok(new { items });
    }

    [HttpPost("benefits/enrollments")]
    public async Task<IActionResult> EnrollBenefit([FromBody] EnrollBenefitRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var plan = await _db.BenefitPlans.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.BenefitPlanId == request.BenefitPlanId && x.IsActive);
        if (plan == null) return NotFound(new { message = "Active benefit plan was not found." });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (plan.EffectiveFrom > today || (plan.EffectiveTo.HasValue && plan.EffectiveTo < today))
            return Conflict(new { message = "This benefit plan is outside its enrollment period." });
        var existing = await _db.BenefitEnrollments.FirstOrDefaultAsync(x =>
            x.CompanyId == companyId.Value && x.BenefitPlanId == plan.BenefitPlanId && x.UserId == actorId.Value);
        if (existing != null && existing.Status != "declined")
            return Conflict(new { message = "You already have an active or pending enrollment for this plan." });
        if (existing != null)
        {
            existing.Status = "pending";
            existing.Notes = null;
            existing.ReviewedAt = null;
            existing.ReviewedBy = null;
            existing.CreatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "REAPPLY", "BenefitEnrollment", existing.BenefitEnrollmentId, after: new { existing.BenefitPlanId, existing.Status });
            return Ok(new { existing.BenefitEnrollmentId, existing.BenefitPlanId, existing.Status });
        }
        var item = new BenefitEnrollment { CompanyId = companyId.Value, BenefitPlanId = plan.BenefitPlanId, UserId = actorId.Value, Status = "pending", CreatedAt = DateTime.UtcNow };
        _db.BenefitEnrollments.Add(item); await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "ENROLL", "BenefitEnrollment", item.BenefitEnrollmentId, after: new { item.BenefitPlanId, item.Status });
        return StatusCode(201, new { item.BenefitEnrollmentId, item.BenefitPlanId, item.Status });
    }

    [HttpPost("benefits/enrollments/{id:int}/decision")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> DecideBenefitEnrollment(int id, [FromBody] DecideBenefitEnrollmentRequest request)
    {
        var companyId = GetCurrentCompanyId(); var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        var decision = request.Decision?.Trim().ToLowerInvariant() ?? "";
        if (decision is not ("active" or "declined")) return BadRequest(new { message = "Decision must be active or declined." });
        var item = await _db.BenefitEnrollments.FirstOrDefaultAsync(x => x.CompanyId == companyId.Value && x.BenefitEnrollmentId == id);
        if (item == null) return NotFound();
        if (item.Status != "pending") return Conflict(new { message = "Only pending benefit enrollments can be decided." });
        item.Status = decision; item.ReviewedBy = actorId.Value; item.ReviewedAt = DateTime.UtcNow; item.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, decision.ToUpperInvariant(), "BenefitEnrollment", id, after: new { item.Status, item.ReviewedAt });
        return Ok(new { item.BenefitEnrollmentId, item.Status, item.ReviewedAt });
    }

    private bool IsHrAdmin()
        => GetCurrentRole() is string role &&
           (role.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("CEO", StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Admin", StringComparison.OrdinalIgnoreCase));

    private async Task<HashSet<int>> GetVisibleEmployeeIdsAsync(int companyId, int actorId)
    {
        var ids = new HashSet<int> { actorId };
        if (IsHrAdmin())
        {
            foreach (var id in await _db.Users.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive).Select(x => x.UserId).ToListAsync()) ids.Add(id);
            return ids;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var edges = await _db.EmployeeReportingLines.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsPrimary && x.RelationshipType == "line" &&
                        x.EffectiveFrom <= today && (!x.EffectiveTo.HasValue || x.EffectiveTo >= today))
            .Select(x => new { x.ManagerUserId, x.EmployeeUserId }).ToListAsync();
        var queue = new Queue<int>(); queue.Enqueue(actorId);
        while (queue.Count > 0)
        {
            var managerId = queue.Dequeue();
            foreach (var employeeId in edges.Where(x => x.ManagerUserId == managerId).Select(x => x.EmployeeUserId))
            {
                if (ids.Add(employeeId)) queue.Enqueue(employeeId);
            }
        }
        return ids;
    }
}

public sealed record CreateApplicantRequest(string CandidateName, string Email, string Phone, string JobTitle, string? Source, string? Notes);
public sealed record UpdateApplicantStageRequest(string Stage);
public sealed record CreateOnboardingTaskRequest(int EmployeeUserId, string Title, string? Description, DateOnly? DueDate);
public sealed record CreateGoalRequest(int? UserId, string Title, string? Description, string? MetricName, decimal TargetValue, DateOnly StartDate, DateOnly EndDate);
public sealed record UpdateGoalProgressRequest(decimal CurrentValue, string? Status);
public sealed record CreateExpenseRequest(string Category, string? Description, decimal Amount, string CurrencyCode, DateOnly ExpenseDate, string? ReceiptReference);
public sealed record DecideExpenseRequest(string Decision, string? Comment);
public sealed record CreateAssetRequest(string AssetTag, string Name, string? Category, string? SerialNumber, string? Notes);
public sealed record AssignAssetRequest(int EmployeeUserId);
public sealed record CreateBenefitPlanRequest(string Name, string? ProviderName, string? Description, decimal EmployeeContribution, decimal EmployerContribution, string CurrencyCode, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record EnrollBenefitRequest(int BenefitPlanId);
public sealed record DecideBenefitEnrollmentRequest(string Decision, string? Notes);
