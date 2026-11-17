namespace TaskTune.API.Support;

public sealed record ScheduledShiftWindow(DateTime StartUtc, DateTime EndUtc);

public static class AttendanceScheduleCalculator
{
    public static ScheduledShiftWindow BuildUtcWindow(
        DateOnly workDate,
        TimeOnly startTime,
        TimeOnly endTime,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        if (startTime == endTime)
            throw new ArgumentException("Shift start and end times must be different.");

        var endDate = endTime <= startTime ? workDate.AddDays(1) : workDate;
        var localStart = DateTime.SpecifyKind(workDate.ToDateTime(startTime), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(endDate.ToDateTime(endTime), DateTimeKind.Unspecified);

        var startUtc = ConvertLocalToUtc(localStart, timeZone, preferEarlierUtc: true);
        var endUtc = ConvertLocalToUtc(localEnd, timeZone, preferEarlierUtc: false);
        if (endUtc <= startUtc)
            throw new InvalidOperationException("The configured shift does not produce a positive UTC duration in this time zone.");

        return new ScheduledShiftWindow(startUtc, endUtc);
    }

    public static int GetWeekdayMask(DayOfWeek dayOfWeek) => 1 << (int)dayOfWeek;

    public static int CalculateLateMinutes(DateTime scheduledStartUtc, DateTime checkInUtc, int graceMinutes)
    {
        if (graceMinutes < 0) throw new ArgumentOutOfRangeException(nameof(graceMinutes));
        var late = (checkInUtc - scheduledStartUtc).TotalMinutes;
        return late <= graceMinutes ? 0 : Math.Max(0, (int)Math.Ceiling(late - graceMinutes));
    }

    public static int CalculateEarlyLeaveMinutes(DateTime scheduledEndUtc, DateTime checkOutUtc, int graceMinutes)
    {
        if (graceMinutes < 0) throw new ArgumentOutOfRangeException(nameof(graceMinutes));
        var early = (scheduledEndUtc - checkOutUtc).TotalMinutes;
        return early <= graceMinutes ? 0 : Math.Max(0, (int)Math.Ceiling(early - graceMinutes));
    }

    public static int CalculateWorkedMinutes(DateTime checkInUtc, DateTime checkOutUtc, int breakMinutes)
    {
        if (breakMinutes < 0) throw new ArgumentOutOfRangeException(nameof(breakMinutes));
        if (checkOutUtc < checkInUtc)
            throw new ArgumentException("Check-out cannot be earlier than check-in.");
        return Math.Max(0, (int)Math.Floor((checkOutUtc - checkInUtc).TotalMinutes) - breakMinutes);
    }

    public static string ResolveAttendanceStatus(int lateMinutes, int earlyLeaveMinutes)
    {
        if (lateMinutes < 0 || earlyLeaveMinutes < 0)
            throw new ArgumentOutOfRangeException("Attendance metrics cannot be negative.");

        if (lateMinutes > 0 && earlyLeaveMinutes > 0) return "late_and_early";
        if (lateMinutes > 0) return "late";
        if (earlyLeaveMinutes > 0) return "early_leave";
        return "present";
    }

    private static DateTime ConvertLocalToUtc(
        DateTime localDateTime,
        TimeZoneInfo timeZone,
        bool preferEarlierUtc)
    {
        // A local time inside a daylight-saving gap does not exist. Move the planned
        // boundary forward to the first valid local minute rather than inventing an offset.
        var adjusted = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        var attempts = 0;
        while (timeZone.IsInvalidTime(adjusted) && attempts < 180)
        {
            adjusted = adjusted.AddMinutes(1);
            attempts++;
        }

        if (timeZone.IsInvalidTime(adjusted))
            throw new InvalidOperationException("The shift boundary falls inside an invalid daylight-saving time range.");

        TimeSpan offset;
        if (timeZone.IsAmbiguousTime(adjusted))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(adjusted);
            // Larger offset produces the earlier UTC instant; use the earlier instant
            // for shift start and the later instant for shift end.
            offset = preferEarlierUtc ? offsets.Max() : offsets.Min();
        }
        else
        {
            offset = timeZone.GetUtcOffset(adjusted);
        }

        return new DateTimeOffset(adjusted, offset).UtcDateTime;
    }
}
