using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/leave")]
[RequireModule("leave")]
[Authorize]
public sealed class LeaveController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public LeaveController(TaskTuneDbContext db) => _db = db;

    [HttpGet("types")]
    public async Task<IActionResult> GetLeaveTypes()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var items = await _db.LeaveTypes.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.LeaveTypeId, x.Code, x.Name, x.Description, x.IsPaid,
                x.RequiresAttachment, x.RequiresApproval, x.AnnualAllowanceDays,
                x.MaximumConsecutiveDays
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPost("types")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateLeaveType([FromBody] SaveLeaveTypeRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        var code = request.Code?.Trim().ToLowerInvariant();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50 ||
            string.IsNullOrWhiteSpace(name) || name.Length > 120 ||
            request.AnnualAllowanceDays < 0 || request.MaximumConsecutiveDays < 0)
            return BadRequest(new { message = "Leave type code, name and allowance values are invalid." });

        if (await _db.LeaveTypes.AnyAsync(x => x.CompanyId == companyId.Value && x.Code == code))
            return Conflict(new { message = "A leave type with this code already exists." });

        var now = DateTime.UtcNow;
        var leaveType = new LeaveType
        {
            CompanyId = companyId.Value,
            Code = code,
            Name = name,
            Description = request.Description?.Trim(),
            IsPaid = request.IsPaid,
            RequiresAttachment = request.RequiresAttachment,
            RequiresApproval = request.RequiresApproval,
            AnnualAllowanceDays = request.AnnualAllowanceDays,
            MaximumConsecutiveDays = request.MaximumConsecutiveDays,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.LeaveTypes.Add(leaveType);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "LeaveType", leaveType.LeaveTypeId,
            after: new { leaveType.Code, leaveType.Name, leaveType.AnnualAllowanceDays, leaveType.IsPaid });

        return CreatedAtAction(nameof(GetLeaveTypes), new { id = leaveType.LeaveTypeId },
            new { leaveType.LeaveTypeId, leaveType.Code, leaveType.Name });
    }

    [HttpPut("types/{leaveTypeId:int}")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> UpdateLeaveType(int leaveTypeId, [FromBody] SaveLeaveTypeRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(x =>
            x.LeaveTypeId == leaveTypeId && x.CompanyId == companyId.Value);
        if (leaveType == null) return NotFound();

        var code = request.Code?.Trim().ToLowerInvariant();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50 ||
            string.IsNullOrWhiteSpace(name) || name.Length > 120 ||
            request.AnnualAllowanceDays < 0 || request.MaximumConsecutiveDays < 0)
            return BadRequest(new { message = "Leave type code, name and allowance values are invalid." });

        if (await _db.LeaveTypes.AnyAsync(x =>
                x.CompanyId == companyId.Value && x.LeaveTypeId != leaveTypeId && x.Code == code))
            return Conflict(new { message = "A leave type with this code already exists." });

        var before = new { leaveType.Code, leaveType.Name, leaveType.AnnualAllowanceDays, leaveType.IsActive };
        leaveType.Code = code;
        leaveType.Name = name;
        leaveType.Description = request.Description?.Trim();
        leaveType.IsPaid = request.IsPaid;
        leaveType.RequiresAttachment = request.RequiresAttachment;
        leaveType.RequiresApproval = request.RequiresApproval;
        leaveType.AnnualAllowanceDays = request.AnnualAllowanceDays;
        leaveType.MaximumConsecutiveDays = request.MaximumConsecutiveDays;
        leaveType.IsActive = request.IsActive;
        leaveType.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "UPDATE", "LeaveType", leaveTypeId,
            before: before,
            after: new { leaveType.Code, leaveType.Name, leaveType.AnnualAllowanceDays, leaveType.IsActive });
        return Ok(new { leaveType.LeaveTypeId, leaveType.Code, leaveType.Name, leaveType.IsActive });
    }

    [HttpGet("balances/me")]
    public async Task<IActionResult> GetMyBalances([FromQuery] int? year)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        return await GetBalances(companyId.Value, userId.Value, year ?? DateTime.UtcNow.Year);
    }

    [HttpGet("balances/{employeeUserId:int}")]
    [Authorize(Roles = "HR,Manager,CEO,Admin,Supervisor")]
    public async Task<IActionResult> GetEmployeeBalances(int employeeUserId, [FromQuery] int? year)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        if (!await CanViewEmployeeAsync(_db, employeeUserId)) return NotFound();
        return await GetBalances(companyId.Value, employeeUserId, year ?? DateTime.UtcNow.Year);
    }

    [HttpPut("balances/{employeeUserId:int}/{leaveTypeId:int}/{year:int}")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> SetBalance(
        int employeeUserId, int leaveTypeId, int year, [FromBody] SetLeaveBalanceRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (year is < 2000 or > 2200 ||
            request.AllocatedDays < 0 || request.CarriedOverDays < 0)
            return BadRequest(new { message = "Year or balance values are invalid." });

        var employeeExists = await _db.Users.AnyAsync(x =>
            x.UserId == employeeUserId && x.CompanyId == companyId.Value && x.IsActive);
        var typeExists = await _db.LeaveTypes.AnyAsync(x =>
            x.LeaveTypeId == leaveTypeId && x.CompanyId == companyId.Value && x.IsActive);
        if (!employeeExists || !typeExists)
            return NotFound(new { message = "Employee or leave type was not found in this company." });

        var balance = await _db.LeaveBalances.FirstOrDefaultAsync(x =>
            x.CompanyId == companyId.Value &&
            x.UserId == employeeUserId &&
            x.LeaveTypeId == leaveTypeId &&
            x.Year == year);

        if (balance == null)
        {
            balance = new LeaveBalance
            {
                CompanyId = companyId.Value,
                UserId = employeeUserId,
                LeaveTypeId = leaveTypeId,
                Year = year,
                AllocatedDays = request.AllocatedDays,
                CarriedOverDays = request.CarriedOverDays,
                UsedDays = 0,
                PendingDays = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.LeaveBalances.Add(balance);
        }
        else
        {
            if (request.AllocatedDays + request.CarriedOverDays < balance.UsedDays + balance.PendingDays)
                return Conflict(new { message = "New allocation cannot be lower than days already used or pending." });
            balance.AllocatedDays = request.AllocatedDays;
            balance.CarriedOverDays = request.CarriedOverDays;
            balance.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "UPSERT", "LeaveBalance", balance.LeaveBalanceId,
            after: new { employeeUserId, leaveTypeId, year, balance.AllocatedDays, balance.CarriedOverDays, balance.UsedDays, balance.PendingDays });

        return Ok(new
        {
            balance.LeaveBalanceId, balance.UserId, balance.LeaveTypeId, balance.Year,
            balance.AllocatedDays, balance.CarriedOverDays, balance.UsedDays, balance.PendingDays,
            AvailableDays = balance.AllocatedDays + balance.CarriedOverDays - balance.UsedDays - balance.PendingDays
        });
    }

    [HttpGet("requests/me")]
    public async Task<IActionResult> GetMyRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        if (!ValidPaging(page, pageSize)) return BadRequest(new { message = "Invalid paging values." });

        var query = _db.LeaveRequests.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.UserId == userId.Value);
        return Ok(await ProjectRequests(query, page, pageSize));
    }

    [HttpGet("requests/inbox")]
    [Authorize(Roles = "HR,Manager,CEO,Admin,Supervisor")]
    public async Task<IActionResult> GetApprovalInbox([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        var role = GetCurrentRole();
        if (userId == null || companyId == null || role == null) return Unauthorized();
        if (!ValidPaging(page, pageSize)) return BadRequest(new { message = "Invalid paging values." });

        var query = _db.LeaveRequests.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.Status == "pending");

        if (role is "HR" or "CEO" or "Admin")
            query = query.Where(x => x.ApproverUserId == userId.Value || x.ApproverUserId == null);
        else
            query = query.Where(x => x.ApproverUserId == userId.Value);

        return Ok(await ProjectRequests(query, page, pageSize));
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateLeaveRequest request)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate.Year is < 2000 or > 2200 ||
            request.EndDate.Year is < 2000 or > 2200 ||
            request.EndDate < request.StartDate ||
            request.StartDate < today)
            return BadRequest(new { message = "Leave requests must have valid future-or-current dates." });
        if (request.EndDate.DayNumber - request.StartDate.DayNumber > 366)
            return BadRequest(new { message = "The requested date range cannot exceed 367 calendar days." });
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000)
            return BadRequest(new { message = "A reason of up to 2,000 characters is required." });

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(x =>
            x.LeaveTypeId == request.LeaveTypeId &&
            x.CompanyId == companyId.Value &&
            x.IsActive);
        if (leaveType == null) return NotFound(new { message = "Leave type not found." });
        if (leaveType.RequiresAttachment)
            return BadRequest(new { message = "This leave type requires supporting evidence. Evidence upload must be enabled for this company before requests can be submitted." });

        var holidayRecords = await _db.CompanyHolidays.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.IsActive &&
                        ((x.HolidayDate >= request.StartDate && x.HolidayDate <= request.EndDate) || x.IsRecurring))
            .Select(x => new { x.HolidayDate, x.IsRecurring })
            .ToListAsync();

        var holidays = new HashSet<DateOnly>();
        for (var day = request.StartDate; day <= request.EndDate; day = day.AddDays(1))
        {
            if (holidayRecords.Any(h => h.HolidayDate == day ||
                                        (h.IsRecurring && h.HolidayDate.Month == day.Month && h.HolidayDate.Day == day.Day)))
                holidays.Add(day);
        }

        var calendarSettings = await WorkCalendarSettingsStore.LoadAsync(_db, companyId.Value);
        var weekendDays = calendarSettings.WeekendDays.Select(day => (DayOfWeek)day).ToArray();
        var daysByYear = LeaveCalendarCalculator.CountWorkingDaysByYear(
            request.StartDate, request.EndDate, holidays, weekendDays);
        var totalDays = daysByYear.Values.Sum();
        if (totalDays <= 0)
            return BadRequest(new { message = "The selected date range contains no working days." });
        if (leaveType.MaximumConsecutiveDays > 0 && totalDays > leaveType.MaximumConsecutiveDays)
            return BadRequest(new { message = $"This leave type permits at most {leaveType.MaximumConsecutiveDays:0.##} working days per request." });

        var overlaps = await _db.LeaveRequests.AnyAsync(x =>
            x.CompanyId == companyId.Value &&
            x.UserId == userId.Value &&
            (x.Status == "pending" || x.Status == "approved") &&
            x.StartDate <= request.EndDate &&
            x.EndDate >= request.StartDate);
        if (overlaps)
            return Conflict(new { message = "This request overlaps another pending or approved leave request." });

        var years = daysByYear.Keys.ToList();
        var balances = await _db.LeaveBalances.Where(x =>
            x.CompanyId == companyId.Value &&
            x.UserId == userId.Value &&
            x.LeaveTypeId == leaveType.LeaveTypeId &&
            years.Contains(x.Year))
            .ToListAsync();

        var allocations = new List<(LeaveBalance Balance, decimal Days)>();
        foreach (var yearDays in daysByYear.OrderBy(x => x.Key))
        {
            var balance = balances.FirstOrDefault(x => x.Year == yearDays.Key);
            if (balance == null)
                return Conflict(new { message = $"No leave balance has been allocated for {yearDays.Key}. Contact HR." });

            var available = balance.AllocatedDays + balance.CarriedOverDays - balance.UsedDays - balance.PendingDays;
            if (available < yearDays.Value)
                return Conflict(new
                {
                    message = $"Insufficient leave balance for {yearDays.Key}. Available: {available:0.##} days; required: {yearDays.Value:0.##} days."
                });
            allocations.Add((balance, yearDays.Value));
        }

        var approverId = await (
            from line in _db.EmployeeReportingLines
            join manager in _db.Users on line.ManagerUserId equals manager.UserId
            where line.CompanyId == companyId.Value &&
                  line.EmployeeUserId == userId.Value &&
                  line.IsPrimary &&
                  line.RelationshipType == "line" &&
                  line.EffectiveFrom <= today &&
                  (!line.EffectiveTo.HasValue || line.EffectiveTo >= today) &&
                  manager.CompanyId == companyId.Value &&
                  manager.IsActive
            select (int?)manager.UserId)
            .FirstOrDefaultAsync();

        var leaveRequest = new LeaveRequest
        {
            CompanyId = companyId.Value,
            UserId = userId.Value,
            LeaveTypeId = leaveType.LeaveTypeId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RequestedDays = totalDays,
            Reason = request.Reason.Trim(),
            Status = leaveType.RequiresApproval ? "pending" : "approved",
            ApproverUserId = approverId,
            DecisionBy = leaveType.RequiresApproval ? null : userId.Value,
            DecisionAt = leaveType.RequiresApproval ? null : DateTime.UtcNow,
            DecisionComment = leaveType.RequiresApproval ? null : "Automatically approved by leave type policy.",
            SubmittedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.LeaveRequests.Add(leaveRequest);
        await _db.SaveChangesAsync();

        foreach (var allocation in allocations)
        {
            if (leaveType.RequiresApproval)
                allocation.Balance.PendingDays += allocation.Days;
            else
                allocation.Balance.UsedDays += allocation.Days;
            allocation.Balance.UpdatedAt = DateTime.UtcNow;

            _db.LeaveRequestBalanceAllocations.Add(new LeaveRequestBalanceAllocation
            {
                CompanyId = companyId.Value,
                LeaveRequestId = leaveRequest.LeaveRequestId,
                LeaveBalanceId = allocation.Balance.LeaveBalanceId,
                LeaveYear = allocation.Balance.Year,
                AllocatedDays = allocation.Days,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "CREATE", "LeaveRequest", leaveRequest.LeaveRequestId,
            after: new
            {
                leaveRequest.LeaveTypeId,
                leaveRequest.StartDate,
                leaveRequest.EndDate,
                leaveRequest.RequestedDays,
                leaveRequest.Status,
                AnnualAllocations = allocations.Select(x => new { x.Balance.Year, x.Days }).ToList()
            });

        return CreatedAtAction(nameof(GetMyRequests), new { }, new
        {
            leaveRequest.LeaveRequestId,
            leaveRequest.StartDate,
            leaveRequest.EndDate,
            leaveRequest.RequestedDays,
            leaveRequest.Status
        });
    }

    [HttpPost("requests/{leaveRequestId:int}/decision")]
    [Authorize(Roles = "HR,Manager,CEO,Admin,Supervisor")]
    public async Task<IActionResult> DecideRequest(int leaveRequestId, [FromBody] LeaveDecisionRequest request)
    {
        var actorId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        var role = GetCurrentRole();
        if (actorId == null || companyId == null || role == null) return Unauthorized();
        if (request.Approve && request.Comment?.Length > 2000)
            return BadRequest(new { message = "Decision comment is too long." });

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var leaveRequest = await _db.LeaveRequests.FirstOrDefaultAsync(x =>
            x.LeaveRequestId == leaveRequestId &&
            x.CompanyId == companyId.Value);
        if (leaveRequest == null) return NotFound();
        if (leaveRequest.Status != "pending")
            return Conflict(new { message = "Only pending leave requests can be decided." });

        var isPrivilegedHr = role is "HR" or "CEO" or "Admin";
        if (!isPrivilegedHr && leaveRequest.ApproverUserId != actorId.Value)
            return Forbid();
        if (leaveRequest.UserId == actorId.Value)
            return Forbid();

        var allocations = await _db.LeaveRequestBalanceAllocations
            .Where(x => x.CompanyId == companyId.Value && x.LeaveRequestId == leaveRequestId)
            .ToListAsync();
        if (allocations.Count == 0 || allocations.Sum(x => x.AllocatedDays) != leaveRequest.RequestedDays)
            return Conflict(new { message = "The leave request has no valid annual balance allocation ledger. HR must reconcile it." });

        var balanceIds = allocations.Select(x => x.LeaveBalanceId).Distinct().ToList();
        var balances = await _db.LeaveBalances
            .Where(x => x.CompanyId == companyId.Value && x.UserId == leaveRequest.UserId && balanceIds.Contains(x.LeaveBalanceId))
            .ToListAsync();
        if (balances.Count != balanceIds.Count)
            return Conflict(new { message = "One or more annual leave balances are missing. HR must reconcile the request." });

        foreach (var allocation in allocations)
        {
            var annualBalance = balances.First(x => x.LeaveBalanceId == allocation.LeaveBalanceId);
            if (annualBalance.PendingDays < allocation.AllocatedDays)
                return Conflict(new { message = $"The pending balance for {allocation.LeaveYear} is inconsistent. HR must reconcile it." });

            annualBalance.PendingDays -= allocation.AllocatedDays;
            if (request.Approve)
                annualBalance.UsedDays += allocation.AllocatedDays;
            annualBalance.UpdatedAt = DateTime.UtcNow;
        }

        var before = new { leaveRequest.Status, leaveRequest.DecisionBy, leaveRequest.DecisionAt };
        leaveRequest.Status = request.Approve ? "approved" : "rejected";
        leaveRequest.DecisionBy = actorId.Value;
        leaveRequest.DecisionAt = DateTime.UtcNow;
        leaveRequest.DecisionComment = request.Comment?.Trim();
        leaveRequest.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The leave balance was changed by another request. Refresh and retry." });
        }

        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, request.Approve ? "APPROVE" : "REJECT",
            "LeaveRequest", leaveRequest.LeaveRequestId,
            before: before,
            after: new { leaveRequest.Status, leaveRequest.DecisionBy, leaveRequest.DecisionAt, leaveRequest.DecisionComment });

        return Ok(new { leaveRequest.LeaveRequestId, leaveRequest.Status, leaveRequest.DecisionAt });
    }

    [HttpPost("requests/{leaveRequestId:int}/cancel")]
    public async Task<IActionResult> CancelRequest(int leaveRequestId, [FromBody] CancelLeaveRequest? request)
    {
        var actorId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (actorId == null || companyId == null) return Unauthorized();

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var leaveRequest = await _db.LeaveRequests.FirstOrDefaultAsync(x =>
            x.LeaveRequestId == leaveRequestId &&
            x.CompanyId == companyId.Value &&
            x.UserId == actorId.Value);
        if (leaveRequest == null) return NotFound();
        if (leaveRequest.Status != "pending")
            return Conflict(new { message = "Only pending leave requests can be cancelled by the employee." });

        var allocations = await _db.LeaveRequestBalanceAllocations
            .Where(x => x.CompanyId == companyId.Value && x.LeaveRequestId == leaveRequestId)
            .ToListAsync();
        if (allocations.Count == 0 || allocations.Sum(x => x.AllocatedDays) != leaveRequest.RequestedDays)
            return Conflict(new { message = "The leave request has no valid annual balance allocation ledger. HR must reconcile it." });

        var balanceIds = allocations.Select(x => x.LeaveBalanceId).Distinct().ToList();
        var balances = await _db.LeaveBalances
            .Where(x => x.CompanyId == companyId.Value && x.UserId == actorId.Value && balanceIds.Contains(x.LeaveBalanceId))
            .ToListAsync();
        if (balances.Count != balanceIds.Count)
            return Conflict(new { message = "One or more annual leave balances are missing. HR must reconcile the request." });

        foreach (var allocation in allocations)
        {
            var annualBalance = balances.First(x => x.LeaveBalanceId == allocation.LeaveBalanceId);
            if (annualBalance.PendingDays < allocation.AllocatedDays)
                return Conflict(new { message = $"The pending balance for {allocation.LeaveYear} is inconsistent. HR must reconcile it." });
            annualBalance.PendingDays -= allocation.AllocatedDays;
            annualBalance.UpdatedAt = DateTime.UtcNow;
        }

        var before = new
        {
            leaveRequest.Status,
            PendingDaysByYear = balances.Select(x => new { x.Year, x.PendingDays }).ToList()
        };
        leaveRequest.Status = "cancelled";
        leaveRequest.DecisionComment = request?.Reason?.Trim();
        leaveRequest.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The leave balance was changed by another request. Refresh and retry." });
        }

        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CANCEL", "LeaveRequest", leaveRequestId,
            before: before,
            after: new
            {
                leaveRequest.Status,
                PendingDaysByYear = balances.Select(x => new { x.Year, x.PendingDays }).ToList()
            });
        return Ok(new { leaveRequest.LeaveRequestId, leaveRequest.Status });
    }

    [HttpGet("calendar-settings")]
    public async Task<IActionResult> GetCalendarSettings()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        var settings = await WorkCalendarSettingsStore.LoadAsync(_db, companyId.Value);
        return Ok(new
        {
            weekendDays = settings.WeekendDays,
            dayNames = settings.WeekendDays.Select(day => ((DayOfWeek)day).ToString()).ToList()
        });
    }

    [HttpPut("calendar-settings")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> UpdateCalendarSettings([FromBody] UpdateWorkCalendarRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        try
        {
            var before = await WorkCalendarSettingsStore.LoadAsync(_db, companyId.Value);
            var saved = await WorkCalendarSettingsStore.SaveAsync(_db, companyId.Value, request.WeekendDays);
            await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "UPDATE", "CompanyWorkCalendar", companyId.Value,
                before: new { before.WeekendDays },
                after: new { saved.WeekendDays });

            return Ok(new { weekendDays = saved.WeekendDays });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("holidays")]
    public async Task<IActionResult> GetHolidays([FromQuery] int? year)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        var selectedYear = year ?? DateTime.UtcNow.Year;
        if (selectedYear is < 2000 or > 2200)
            return BadRequest(new { message = "Year must be between 2000 and 2200." });

        var records = await _db.CompanyHolidays.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.IsActive &&
                        (x.HolidayDate.Year == selectedYear || x.IsRecurring))
            .OrderBy(x => x.HolidayDate)
            .ToListAsync();

        var items = records.Select(record =>
        {
            DateOnly displayDate;
            try
            {
                displayDate = record.IsRecurring
                    ? new DateOnly(selectedYear, record.HolidayDate.Month, record.HolidayDate.Day)
                    : record.HolidayDate;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null; // e.g. a recurring February 29 holiday in a non-leap year.
            }

            return new
            {
                record.CompanyHolidayId,
                record.Name,
                HolidayDate = displayDate,
                record.IsRecurring
            };
        })
        .Where(item => item != null)
        .GroupBy(item => item!.HolidayDate)
        .Select(group => group.First()!)
        .OrderBy(item => item.HolidayDate)
        .ToList();

        return Ok(new { items });
    }

    [HttpPost("holidays")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateHoliday([FromBody] CreateCompanyHolidayRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
            return BadRequest(new { message = "Holiday name is required and must not exceed 160 characters." });

        var existing = await _db.CompanyHolidays.AnyAsync(x =>
            x.CompanyId == companyId.Value && x.IsActive &&
            (x.HolidayDate == request.HolidayDate ||
             (x.IsRecurring && x.HolidayDate.Month == request.HolidayDate.Month &&
              x.HolidayDate.Day == request.HolidayDate.Day) ||
             (request.IsRecurring && x.HolidayDate.Month == request.HolidayDate.Month &&
              x.HolidayDate.Day == request.HolidayDate.Day)));

        if (existing)
            return Conflict(new { message = "A holiday is already configured for this date." });

        var holiday = new CompanyHoliday
        {
            CompanyId = companyId.Value,
            Name = request.Name.Trim(),
            HolidayDate = request.HolidayDate,
            IsRecurring = request.IsRecurring,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.CompanyHolidays.Add(holiday);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "CompanyHoliday", holiday.CompanyHolidayId,
            after: new { holiday.Name, holiday.HolidayDate, holiday.IsRecurring });
        return CreatedAtAction(nameof(GetHolidays), new { year = holiday.HolidayDate.Year },
            new { holiday.CompanyHolidayId, holiday.Name, holiday.HolidayDate, holiday.IsRecurring });
    }

    private async Task<IActionResult> GetBalances(int companyId, int userId, int year)
    {
        if (year is < 2000 or > 2200)
            return BadRequest(new { message = "Year must be between 2000 and 2200." });

        var items = await (
            from balance in _db.LeaveBalances.AsNoTracking()
            join leaveType in _db.LeaveTypes.AsNoTracking() on balance.LeaveTypeId equals leaveType.LeaveTypeId
            where balance.CompanyId == companyId &&
                  balance.UserId == userId &&
                  balance.Year == year &&
                  leaveType.CompanyId == companyId &&
                  leaveType.IsActive
            orderby leaveType.Name
            select new
            {
                balance.LeaveBalanceId,
                balance.LeaveTypeId,
                LeaveType = leaveType.Name,
                leaveType.Code,
                balance.Year,
                balance.AllocatedDays,
                balance.CarriedOverDays,
                balance.UsedDays,
                balance.PendingDays,
                AvailableDays = balance.AllocatedDays + balance.CarriedOverDays - balance.UsedDays - balance.PendingDays
            })
            .ToListAsync();

        return Ok(new { userId, year, items });
    }

    private async Task<object> ProjectRequests(IQueryable<LeaveRequest> query, int page, int pageSize)
    {
        var total = await query.CountAsync();
        var items = await (
            from request in query
            join type in _db.LeaveTypes.AsNoTracking() on request.LeaveTypeId equals type.LeaveTypeId
            join employee in _db.Users.AsNoTracking() on request.UserId equals employee.UserId
            join approver in _db.Users.AsNoTracking() on request.ApproverUserId equals approver.UserId into approvers
            from approver in approvers.DefaultIfEmpty()
            orderby request.SubmittedAt descending
            select new
            {
                request.LeaveRequestId,
                request.UserId,
                EmployeeName = employee.FullName ?? (employee.FirstName + " " + employee.LastName),
                request.LeaveTypeId,
                LeaveType = type.Name,
                request.StartDate,
                request.EndDate,
                request.RequestedDays,
                request.Reason,
                request.Status,
                request.ApproverUserId,
                ApproverName = approver != null ? (approver.FullName ?? (approver.FirstName + " " + approver.LastName)) : null,
                request.DecisionBy,
                request.DecisionAt,
                request.DecisionComment,
                request.SubmittedAt
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new { items, totalCount = total, page, pageSize };
    }

    private static bool ValidPaging(int page, int pageSize) => page >= 1 && pageSize is >= 1 and <= 100;
}

public sealed record SaveLeaveTypeRequest(
    string Code, string Name, string? Description,
    bool IsPaid, bool RequiresAttachment, bool RequiresApproval,
    decimal AnnualAllowanceDays, decimal MaximumConsecutiveDays,
    bool IsActive = true);

public sealed record SetLeaveBalanceRequest(decimal AllocatedDays, decimal CarriedOverDays);
public sealed record CreateLeaveRequest(int LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string Reason);
public sealed record LeaveDecisionRequest(bool Approve, string? Comment);
public sealed record CancelLeaveRequest(string? Reason);
public sealed record CreateCompanyHolidayRequest(string Name, DateOnly HolidayDate, bool IsRecurring);
public sealed record UpdateWorkCalendarRequest(List<int> WeekendDays);
