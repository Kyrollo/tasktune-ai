using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/attendance")]
[RequireModule("attendance")]
[Authorize]
public sealed class WorkShiftsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public WorkShiftsController(TaskTuneDbContext db) => _db = db;

    [HttpGet("shifts")]
    public async Task<IActionResult> GetShifts()
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();

        var items = await _db.WorkShifts.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.IsActive)
            .OrderBy(x => x.StartTime)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.WorkShiftId,
                x.Name,
                x.StartTime,
                x.EndTime,
                x.BreakMinutes,
                x.GraceMinutes,
                x.IsActive,
                IsOvernight = x.EndTime <= x.StartTime
            })
            .ToListAsync();

        return Ok(new { items });
    }

    [HttpPost("shifts")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CreateShift([FromBody] SaveWorkShiftRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        var validationError = ValidateShift(request);
        if (validationError != null) return BadRequest(new { message = validationError });

        var name = request.Name.Trim();
        if (await _db.WorkShifts.AnyAsync(x => x.CompanyId == companyId.Value && x.Name == name))
            return Conflict(new { message = "A shift with this name already exists." });

        var now = DateTime.UtcNow;
        var shift = new WorkShift
        {
            CompanyId = companyId.Value,
            Name = name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            BreakMinutes = request.BreakMinutes,
            GraceMinutes = request.GraceMinutes,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.WorkShifts.Add(shift);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CREATE", "WorkShift", shift.WorkShiftId,
            after: new { shift.Name, shift.StartTime, shift.EndTime, shift.BreakMinutes, shift.GraceMinutes });

        return CreatedAtAction(nameof(GetShifts), new { }, new
        {
            shift.WorkShiftId,
            shift.Name,
            shift.StartTime,
            shift.EndTime,
            shift.BreakMinutes,
            shift.GraceMinutes,
            IsOvernight = shift.EndTime <= shift.StartTime
        });
    }

    [HttpPut("shifts/{workShiftId:int}")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> UpdateShift(int workShiftId, [FromBody] SaveWorkShiftRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        var validationError = ValidateShift(request);
        if (validationError != null) return BadRequest(new { message = validationError });

        var shift = await _db.WorkShifts.FirstOrDefaultAsync(x =>
            x.WorkShiftId == workShiftId && x.CompanyId == companyId.Value);
        if (shift == null) return NotFound();

        var name = request.Name.Trim();
        if (await _db.WorkShifts.AnyAsync(x =>
                x.CompanyId == companyId.Value &&
                x.WorkShiftId != workShiftId &&
                x.Name == name))
            return Conflict(new { message = "A shift with this name already exists." });

        var before = new
        {
            shift.Name,
            shift.StartTime,
            shift.EndTime,
            shift.BreakMinutes,
            shift.GraceMinutes,
            shift.IsActive
        };

        shift.Name = name;
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.BreakMinutes = request.BreakMinutes;
        shift.GraceMinutes = request.GraceMinutes;
        shift.IsActive = request.IsActive;
        shift.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "UPDATE", "WorkShift", workShiftId,
            before: before,
            after: new
            {
                shift.Name,
                shift.StartTime,
                shift.EndTime,
                shift.BreakMinutes,
                shift.GraceMinutes,
                shift.IsActive
            });

        return Ok(new { shift.WorkShiftId, shift.Name, shift.StartTime, shift.EndTime, shift.IsActive });
    }

    [HttpGet("assignments/me")]
    public async Task<IActionResult> GetMyAssignments()
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();
        return await GetAssignments(companyId.Value, userId.Value);
    }

    [HttpGet("assignments/{employeeUserId:int}")]
    [Authorize(Roles = "HR,Manager,CEO,Admin,Supervisor")]
    public async Task<IActionResult> GetEmployeeAssignments(int employeeUserId)
    {
        var companyId = GetCurrentCompanyId();
        if (companyId == null) return Unauthorized();
        if (!await CanViewEmployeeAsync(_db, employeeUserId)) return NotFound();
        return await GetAssignments(companyId.Value, employeeUserId);
    }

    [HttpPost("assignments")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> AssignShift([FromBody] CreateShiftAssignmentRequest request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        if (request.DaysOfWeekMask is < 1 or > 127)
            return BadRequest(new { message = "Select at least one scheduled weekday." });
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom)
            return BadRequest(new { message = "Assignment end date cannot be earlier than its start date." });

        var employeeExists = await _db.Users.AnyAsync(x =>
            x.UserId == request.UserId && x.CompanyId == companyId.Value && x.IsActive);
        if (!employeeExists) return NotFound(new { message = "An active employee in this company was not found." });

        var shiftExists = await _db.WorkShifts.AnyAsync(x =>
            x.WorkShiftId == request.WorkShiftId && x.CompanyId == companyId.Value && x.IsActive);
        if (!shiftExists) return NotFound(new { message = "An active work shift in this company was not found." });

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var end = request.EffectiveTo ?? DateOnly.MaxValue;
        var overlap = await _db.EmployeeShiftAssignments.AnyAsync(x =>
            x.CompanyId == companyId.Value &&
            x.UserId == request.UserId &&
            (x.DaysOfWeekMask & request.DaysOfWeekMask) != 0 &&
            x.EffectiveFrom <= end &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo >= request.EffectiveFrom));
        if (overlap)
            return Conflict(new { message = "This employee already has an overlapping shift assignment for one or more selected weekdays." });

        var assignment = new EmployeeShiftAssignment
        {
            CompanyId = companyId.Value,
            UserId = request.UserId,
            WorkShiftId = request.WorkShiftId,
            DaysOfWeekMask = request.DaysOfWeekMask,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedBy = actorId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _db.EmployeeShiftAssignments.Add(assignment);
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "ASSIGN", "EmployeeShiftAssignment",
            assignment.EmployeeShiftAssignmentId,
            after: new
            {
                assignment.UserId,
                assignment.WorkShiftId,
                assignment.DaysOfWeekMask,
                assignment.EffectiveFrom,
                assignment.EffectiveTo
            });
        await transaction.CommitAsync();

        return CreatedAtAction(nameof(GetEmployeeAssignments), new { employeeUserId = assignment.UserId },
            new
            {
                assignment.EmployeeShiftAssignmentId,
                assignment.UserId,
                assignment.WorkShiftId,
                assignment.DaysOfWeekMask,
                assignment.EffectiveFrom,
                assignment.EffectiveTo
            });
    }

    [HttpPost("assignments/{assignmentId:int}/close")]
    [Authorize(Roles = "HR,CEO,Admin")]
    public async Task<IActionResult> CloseAssignment(int assignmentId, [FromBody] CloseShiftAssignmentRequest? request)
    {
        var companyId = GetCurrentCompanyId();
        var actorId = GetCurrentUserId();
        if (companyId == null || actorId == null) return Unauthorized();

        var assignment = await _db.EmployeeShiftAssignments.FirstOrDefaultAsync(x =>
            x.EmployeeShiftAssignmentId == assignmentId && x.CompanyId == companyId.Value);
        if (assignment == null) return NotFound();

        var closeDate = request?.EffectiveTo ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (closeDate < assignment.EffectiveFrom)
            return BadRequest(new { message = "Assignment end date cannot be earlier than its start date." });
        if (assignment.EffectiveTo.HasValue && assignment.EffectiveTo.Value <= closeDate)
            return Conflict(new { message = "This assignment is already closed." });

        var before = new { assignment.EffectiveTo };
        assignment.EffectiveTo = closeDate;
        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "CLOSE", "EmployeeShiftAssignment", assignmentId,
            before: before, after: new { assignment.EffectiveTo });

        return Ok(new { assignment.EmployeeShiftAssignmentId, assignment.EffectiveTo });
    }

    private async Task<IActionResult> GetAssignments(int companyId, int userId)
    {
        var calendarSettings = await WorkCalendarSettingsStore.LoadAsync(_db, companyId);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(calendarSettings.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var todayMask = AttendanceScheduleCalculator.GetWeekdayMask(today.DayOfWeek);

        var rows = await (
            from assignment in _db.EmployeeShiftAssignments.AsNoTracking()
            join shift in _db.WorkShifts.AsNoTracking() on assignment.WorkShiftId equals shift.WorkShiftId
            where assignment.CompanyId == companyId &&
                  assignment.UserId == userId &&
                  shift.CompanyId == companyId
            orderby assignment.EffectiveFrom descending
            select new
            {
                assignment.EmployeeShiftAssignmentId,
                assignment.UserId,
                assignment.WorkShiftId,
                ShiftName = shift.Name,
                shift.StartTime,
                shift.EndTime,
                shift.BreakMinutes,
                shift.GraceMinutes,
                assignment.DaysOfWeekMask,
                assignment.EffectiveFrom,
                assignment.EffectiveTo
            })
            .ToListAsync();

        var items = rows.Select(assignment => new
        {
            assignment.EmployeeShiftAssignmentId,
            assignment.UserId,
            assignment.WorkShiftId,
            assignment.ShiftName,
            assignment.StartTime,
            assignment.EndTime,
            assignment.BreakMinutes,
            assignment.GraceMinutes,
            assignment.DaysOfWeekMask,
            ScheduledDays = Enumerable.Range(0, 7)
                .Where(day => (assignment.DaysOfWeekMask & (1 << day)) != 0)
                .ToList(),
            assignment.EffectiveFrom,
            assignment.EffectiveTo,
            IsCurrent = assignment.EffectiveFrom <= today &&
                        (!assignment.EffectiveTo.HasValue || assignment.EffectiveTo >= today) &&
                        (assignment.DaysOfWeekMask & todayMask) != 0
        }).ToList();

        return Ok(new { userId, items });
    }

    private static string? ValidateShift(SaveWorkShiftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            return "Shift name is required and must not exceed 120 characters.";
        if (request.StartTime == request.EndTime)
            return "Shift start and end times must be different.";
        if (request.BreakMinutes is < 0 or > 720)
            return "Break duration must be between 0 and 720 minutes.";
        if (request.GraceMinutes is < 0 or > 180)
            return "Grace period must be between 0 and 180 minutes.";
        return null;
    }
}

public sealed record SaveWorkShiftRequest(
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceMinutes,
    bool IsActive = true);

public sealed record CreateShiftAssignmentRequest(
    int UserId,
    int WorkShiftId,
    byte DaysOfWeekMask,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record CloseShiftAssignmentRequest(DateOnly? EffectiveTo);
