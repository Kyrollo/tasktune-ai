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
public sealed class AttendanceController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    public AttendanceController(TaskTuneDbContext db) => _db = db;

    [HttpGet("me/today")]
    public async Task<IActionResult> GetMyToday()
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();

        var (settings, timeZone) = await GetCompanyTimeZoneAsync(companyId.Value);
        if (timeZone == null)
            return StatusCode(503, new { message = "The company time zone is invalid. HR must correct the work calendar settings." });

        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        var workDate = DateOnly.FromDateTime(localNow.DateTime);
        var assignment = await FindShiftForDateAsync(companyId.Value, userId.Value, workDate);
        var record = await _db.AttendanceRecords.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId.Value &&
                                      x.UserId == userId.Value &&
                                      x.WorkDate == workDate);

        return Ok(new
        {
            workDate,
            localNow,
            timeZoneId = settings.TimeZoneId,
            shiftAssigned = assignment != null,
            shift = assignment == null ? null : new
            {
                assignment.WorkShiftId,
                assignment.ShiftName,
                assignment.StartTime,
                assignment.EndTime,
                assignment.BreakMinutes,
                assignment.GraceMinutes,
                assignment.ScheduledStartUtc,
                assignment.ScheduledEndUtc
            },
            attendance = record == null ? null : ToAttendanceResponse(record),
            canClockIn = assignment != null && record?.CheckInUtc == null,
            canClockOut = record?.CheckInUtc != null && record.CheckOutUtc == null
        });
    }

    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn()
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();

        var (_, timeZone) = await GetCompanyTimeZoneAsync(companyId.Value);
        if (timeZone == null)
            return StatusCode(503, new { message = "The company time zone is invalid. HR must correct the work calendar settings." });

        var now = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone);
        var workDate = DateOnly.FromDateTime(localNow);

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existing = await _db.AttendanceRecords.FirstOrDefaultAsync(x =>
            x.CompanyId == companyId.Value &&
            x.UserId == userId.Value &&
            x.WorkDate == workDate);
        if (existing?.CheckInUtc != null)
            return Conflict(new { message = "You have already clocked in for this work date.", attendance = ToAttendanceResponse(existing) });

        var onApprovedLeave = await _db.LeaveRequests.AnyAsync(x =>
            x.CompanyId == companyId.Value &&
            x.UserId == userId.Value &&
            x.Status == "approved" &&
            x.StartDate <= workDate &&
            x.EndDate >= workDate);
        if (onApprovedLeave)
            return Conflict(new { message = "An approved leave request covers today. Contact HR if your attendance needs to be recorded." });

        var assignment = await FindShiftForDateAsync(companyId.Value, userId.Value, workDate);
        if (assignment == null)
            return Conflict(new { message = "No active work shift is assigned to you for today. Contact HR before clocking in." });

        var lateMinutes = AttendanceScheduleCalculator.CalculateLateMinutes(
            assignment.ScheduledStartUtc,
            now,
            assignment.GraceMinutes);

        var record = existing ?? new AttendanceRecord
        {
            CompanyId = companyId.Value,
            UserId = userId.Value,
            WorkShiftId = assignment.WorkShiftId,
            WorkDate = workDate,
            ScheduledStartUtc = assignment.ScheduledStartUtc,
            ScheduledEndUtc = assignment.ScheduledEndUtc,
            BreakMinutes = assignment.BreakMinutes,
            CaptureSource = "web"
        };

        record.CheckInUtc = now;
        record.LateMinutes = lateMinutes;
        record.Status = AttendanceScheduleCalculator.ResolveAttendanceStatus(lateMinutes, 0);
        record.CaptureSource = "web";
        if (existing == null)
            _db.AttendanceRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync();
            await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "CLOCK_IN", "AttendanceRecord", record.AttendanceRecordId,
                after: new { record.WorkDate, record.CheckInUtc, record.LateMinutes, record.Status });
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "Attendance was recorded by another request. Refresh today's attendance." });
        }

        return Ok(new { message = "Clock-in recorded.", attendance = ToAttendanceResponse(record) });
    }

    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut()
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();

        var now = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var record = await _db.AttendanceRecords
            .Where(x => x.CompanyId == companyId.Value &&
                        x.UserId == userId.Value &&
                        x.CheckInUtc != null &&
                        x.CheckOutUtc == null)
            .OrderByDescending(x => x.WorkDate)
            .FirstOrDefaultAsync();

        if (record == null)
            return Conflict(new { message = "There is no open attendance session to clock out from." });
        if (now < record.CheckInUtc!.Value)
            return Conflict(new { message = "The server clock is earlier than the stored check-in time. Contact the administrator." });
        if ((now - record.CheckInUtc.Value).TotalHours > 24)
            return Conflict(new { message = "This attendance session is older than 24 hours and needs an HR correction." });

        var shift = await _db.WorkShifts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkShiftId == record.WorkShiftId && x.CompanyId == companyId.Value);
        if (shift == null) return Conflict(new { message = "The assigned shift no longer exists. HR must reconcile this record." });

        var earlyLeaveMinutes = AttendanceScheduleCalculator.CalculateEarlyLeaveMinutes(
            record.ScheduledEndUtc,
            now,
            shift.GraceMinutes);
        var workedMinutes = AttendanceScheduleCalculator.CalculateWorkedMinutes(
            record.CheckInUtc.Value,
            now,
            record.BreakMinutes);
        var lateMinutes = record.LateMinutes;

        var before = new { record.CheckOutUtc, record.Status, record.EarlyLeaveMinutes, record.WorkedMinutes };
        record.CheckOutUtc = now;
        record.EarlyLeaveMinutes = earlyLeaveMinutes;
        record.WorkedMinutes = workedMinutes;
        record.Status = AttendanceScheduleCalculator.ResolveAttendanceStatus(lateMinutes, earlyLeaveMinutes);

        try
        {
            await _db.SaveChangesAsync();
            await AuditLogWriter.WriteAsync(_db, HttpContext, userId, "CLOCK_OUT", "AttendanceRecord", record.AttendanceRecordId,
                before: before,
                after: new { record.CheckOutUtc, record.Status, record.EarlyLeaveMinutes, record.WorkedMinutes });
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "This attendance record was updated by another request. Refresh and retry." });
        }

        return Ok(new { message = "Clock-out recorded.", attendance = ToAttendanceResponse(record) });
    }

    [HttpGet("me/timesheet")]
    public async Task<IActionResult> GetMyTimesheet(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var userId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (userId == null || companyId == null) return Unauthorized();

        var (startDate, endDate, error) = ValidateDateRange(from, to);
        if (error != null) return BadRequest(new { message = error });

        var items = await _db.AttendanceRecords.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value &&
                        x.UserId == userId.Value &&
                        x.WorkDate >= startDate &&
                        x.WorkDate <= endDate)
            .OrderByDescending(x => x.WorkDate)
            .Select(x => new
            {
                x.AttendanceRecordId,
                x.WorkDate,
                x.WorkShiftId,
                x.ScheduledStartUtc,
                x.ScheduledEndUtc,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.Status,
                x.LateMinutes,
                x.EarlyLeaveMinutes,
                x.WorkedMinutes,
                x.BreakMinutes,
                x.CaptureSource,
                x.IsManualAdjustment,
                x.Notes
            })
            .ToListAsync();

        return Ok(new
        {
            from = startDate,
            to = endDate,
            items,
            summary = new
            {
                RecordedDays = items.Count,
                LateDays = items.Count(x => x.LateMinutes > 0),
                EarlyLeaveDays = items.Count(x => x.EarlyLeaveMinutes > 0),
                TotalWorkedMinutes = items.Sum(x => x.WorkedMinutes)
            }
        });
    }

    [HttpGet("team")]
    [Authorize(Roles = "HR,Manager,CEO,Admin,Supervisor")]
    public async Task<IActionResult> GetTeamAttendance(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? employeeUserId)
    {
        var actorId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (actorId == null || companyId == null) return Unauthorized();

        var (startDate, endDate, error) = ValidateDateRange(from, to);
        if (error != null) return BadRequest(new { message = error });

        var employees = await _db.Users.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.IsActive &&
                        (!employeeUserId.HasValue || x.UserId == employeeUserId.Value))
            .Select(x => new
            {
                x.UserId,
                FullName = x.FullName ?? (x.FirstName + " " + x.LastName),
                x.Email,
                Department = x.Department != null ? x.Department.DepartmentName : null
            })
            .ToListAsync();

        var visibleEmployees = new List<int>();
        foreach (var employee in employees)
        {
            if (await CanViewEmployeeAsync(_db, employee.UserId))
                visibleEmployees.Add(employee.UserId);
        }

        if (employeeUserId.HasValue && !visibleEmployees.Contains(employeeUserId.Value))
            return NotFound();

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value &&
                        visibleEmployees.Contains(x.UserId) &&
                        x.WorkDate >= startDate &&
                        x.WorkDate <= endDate)
            .OrderBy(x => x.WorkDate)
            .ThenBy(x => x.UserId)
            .Select(x => new
            {
                x.AttendanceRecordId,
                x.UserId,
                x.WorkDate,
                x.CheckInUtc,
                x.CheckOutUtc,
                x.Status,
                x.LateMinutes,
                x.EarlyLeaveMinutes,
                x.WorkedMinutes,
                x.IsManualAdjustment
            })
            .ToListAsync();

        var names = employees.ToDictionary(x => x.UserId, x => x);
        var items = records.Select(record => new
        {
            record.AttendanceRecordId,
            record.UserId,
            EmployeeName = names.TryGetValue(record.UserId, out var person) ? person.FullName : "Employee",
            Department = names.TryGetValue(record.UserId, out person) ? person.Department : null,
            record.WorkDate,
            record.CheckInUtc,
            record.CheckOutUtc,
            record.Status,
            record.LateMinutes,
            record.EarlyLeaveMinutes,
            record.WorkedMinutes,
            record.IsManualAdjustment
        });

        return Ok(new
        {
            from = startDate,
            to = endDate,
            items,
            summary = new
            {
                RecordedSessions = records.Count,
                LateSessions = records.Count(x => x.LateMinutes > 0),
                EarlyLeaveSessions = records.Count(x => x.EarlyLeaveMinutes > 0),
                TotalWorkedMinutes = records.Sum(x => x.WorkedMinutes),
                EmployeesWithRecords = records.Select(x => x.UserId).Distinct().Count()
            }
        });
    }

    [HttpPost("records/{attendanceRecordId:int}/adjust")]
    [Authorize(Roles = "HR,Manager,CEO,Admin,Supervisor")]
    public async Task<IActionResult> AdjustAttendance(
        int attendanceRecordId,
        [FromBody] AdjustAttendanceRequest request)
    {
        var actorId = GetCurrentUserId();
        var companyId = GetCurrentCompanyId();
        if (actorId == null || companyId == null) return Unauthorized();

        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(x =>
            x.AttendanceRecordId == attendanceRecordId &&
            x.CompanyId == companyId.Value);
        if (record == null) return NotFound();
        if (!await CanViewEmployeeAsync(_db, record.UserId)) return NotFound();
        if (!request.CheckInUtc.HasValue)
            return BadRequest(new { message = "A corrected check-in time is required. Use HR leave/absence processing for days without a punch." });
        if (request.CheckOutUtc.HasValue && request.CheckOutUtc.Value < request.CheckInUtc.Value)
            return BadRequest(new { message = "Corrected check-out cannot be earlier than corrected check-in." });
        if (request.Notes?.Length > 2000)
            return BadRequest(new { message = "Adjustment notes cannot exceed 2,000 characters." });

        var shift = await _db.WorkShifts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkShiftId == record.WorkShiftId && x.CompanyId == companyId.Value);
        if (shift == null) return Conflict(new { message = "The assigned shift no longer exists." });

        var checkInUtc = request.CheckInUtc.Value.UtcDateTime;
        var checkOutUtc = request.CheckOutUtc?.UtcDateTime;
        var lateMinutes = AttendanceScheduleCalculator.CalculateLateMinutes(
            record.ScheduledStartUtc, checkInUtc, shift.GraceMinutes);
        var earlyMinutes = checkOutUtc.HasValue
            ? AttendanceScheduleCalculator.CalculateEarlyLeaveMinutes(record.ScheduledEndUtc, checkOutUtc.Value, shift.GraceMinutes)
            : 0;
        var workedMinutes = checkOutUtc.HasValue
            ? AttendanceScheduleCalculator.CalculateWorkedMinutes(checkInUtc, checkOutUtc.Value, record.BreakMinutes)
            : 0;

        var before = new
        {
            record.CheckInUtc,
            record.CheckOutUtc,
            record.Status,
            record.LateMinutes,
            record.EarlyLeaveMinutes,
            record.WorkedMinutes,
            record.Notes
        };

        record.CheckInUtc = checkInUtc;
        record.CheckOutUtc = checkOutUtc;
        record.LateMinutes = lateMinutes;
        record.EarlyLeaveMinutes = earlyMinutes;
        record.WorkedMinutes = workedMinutes;
        record.Status = checkOutUtc.HasValue
            ? AttendanceScheduleCalculator.ResolveAttendanceStatus(lateMinutes, earlyMinutes)
            : lateMinutes > 0 ? "late" : "scheduled";
        record.IsManualAdjustment = true;
        record.AdjustedBy = actorId.Value;
        record.AdjustedAt = DateTime.UtcNow;
        record.CaptureSource = "manual";
        record.Notes = string.IsNullOrWhiteSpace(request.Notes) ? "Manual HR adjustment" : request.Notes.Trim();

        await _db.SaveChangesAsync();
        await AuditLogWriter.WriteAsync(_db, HttpContext, actorId, "ADJUST", "AttendanceRecord", attendanceRecordId,
            before: before,
            after: new
            {
                record.UserId,
                record.WorkDate,
                record.CheckInUtc,
                record.CheckOutUtc,
                record.Status,
                record.LateMinutes,
                record.EarlyLeaveMinutes,
                record.WorkedMinutes,
                record.Notes
            });

        return Ok(new { message = "Attendance record adjusted.", attendance = ToAttendanceResponse(record) });
    }

    private async Task<(CompanyWorkCalendarSettings Settings, TimeZoneInfo? TimeZone)> GetCompanyTimeZoneAsync(int companyId)
    {
        var settings = await WorkCalendarSettingsStore.LoadAsync(_db, companyId);
        if (!WorkCalendarSettingsStore.TryGetTimeZone(settings.TimeZoneId, out var timeZone))
            return (settings, null);
        return (settings, timeZone);
    }

    private async Task<AssignedShift?> FindShiftForDateAsync(int companyId, int userId, DateOnly workDate)
    {
        var dayMask = AttendanceScheduleCalculator.GetWeekdayMask(workDate.DayOfWeek);
        var assignment = await (
            from link in _db.EmployeeShiftAssignments.AsNoTracking()
            join shift in _db.WorkShifts.AsNoTracking() on link.WorkShiftId equals shift.WorkShiftId
            where link.CompanyId == companyId &&
                  link.UserId == userId &&
                  shift.CompanyId == companyId &&
                  shift.IsActive &&
                  (link.DaysOfWeekMask & dayMask) != 0 &&
                  link.EffectiveFrom <= workDate &&
                  (!link.EffectiveTo.HasValue || link.EffectiveTo >= workDate)
            orderby link.EffectiveFrom descending
            select new
            {
                shift.WorkShiftId,
                shift.Name,
                shift.StartTime,
                shift.EndTime,
                shift.BreakMinutes,
                shift.GraceMinutes
            })
            .FirstOrDefaultAsync();

        if (assignment == null) return null;

        var (_, timeZone) = await GetCompanyTimeZoneAsync(companyId);
        if (timeZone == null) return null;

        var window = AttendanceScheduleCalculator.BuildUtcWindow(
            workDate, assignment.StartTime, assignment.EndTime, timeZone);

        return new AssignedShift(
            assignment.WorkShiftId,
            assignment.Name,
            assignment.StartTime,
            assignment.EndTime,
            assignment.BreakMinutes,
            assignment.GraceMinutes,
            window.StartUtc,
            window.EndUtc);
    }

    private static object ToAttendanceResponse(AttendanceRecord record) => new
    {
        record.AttendanceRecordId,
        record.WorkDate,
        record.WorkShiftId,
        record.ScheduledStartUtc,
        record.ScheduledEndUtc,
        record.CheckInUtc,
        record.CheckOutUtc,
        record.Status,
        record.LateMinutes,
        record.EarlyLeaveMinutes,
        record.WorkedMinutes,
        record.BreakMinutes,
        record.CaptureSource,
        record.IsManualAdjustment,
        record.Notes
    };

    private static (DateOnly Start, DateOnly End, string? Error) ValidateDateRange(DateOnly? from, DateOnly? to)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        if (end < start)
            return (start, end, "End date cannot be earlier than start date.");
        if (end.DayNumber - start.DayNumber > 92)
            return (start, end, "Attendance queries are limited to 93 calendar days.");
        return (start, end, null);
    }

    private sealed record AssignedShift(
        int WorkShiftId,
        string ShiftName,
        TimeOnly StartTime,
        TimeOnly EndTime,
        int BreakMinutes,
        int GraceMinutes,
        DateTime ScheduledStartUtc,
        DateTime ScheduledEndUtc);
}

public sealed record AdjustAttendanceRequest(DateTimeOffset? CheckInUtc, DateTimeOffset? CheckOutUtc, string? Notes);
