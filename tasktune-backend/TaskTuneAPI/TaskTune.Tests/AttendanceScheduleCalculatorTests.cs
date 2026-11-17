using TaskTune.API.Support;
using Xunit;

namespace TaskTune.Tests;

public sealed class AttendanceScheduleCalculatorTests
{
    [Fact]
    public void BuildsNormalShiftInUtc()
    {
        var window = AttendanceScheduleCalculator.BuildUtcWindow(
            new DateOnly(2026, 11, 17),
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            TimeZoneInfo.Utc);

        Assert.Equal(new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 17, 17, 0, 0, DateTimeKind.Utc), window.EndUtc);
    }

    [Fact]
    public void BuildsOvernightShiftAcrossTwoDates()
    {
        var window = AttendanceScheduleCalculator.BuildUtcWindow(
            new DateOnly(2026, 11, 17),
            new TimeOnly(22, 0),
            new TimeOnly(6, 0),
            TimeZoneInfo.Utc);

        Assert.Equal(new DateTime(2026, 11, 17, 22, 0, 0, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc), window.EndUtc);
        Assert.Equal(8, (window.EndUtc - window.StartUtc).TotalHours);
    }

    [Fact]
    public void AppliesGracePeriodBeforeCountingLateMinutes()
    {
        var scheduled = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        var checkIn = new DateTime(2026, 11, 17, 9, 12, 10, DateTimeKind.Utc);

        Assert.Equal(9, AttendanceScheduleCalculator.CalculateLateMinutes(scheduled, checkIn, 4));
    }

    [Fact]
    public void AppliesGracePeriodBeforeCountingEarlyLeave()
    {
        var scheduledEnd = new DateTime(2026, 11, 17, 17, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 17, 16, 52, 0, DateTimeKind.Utc);

        Assert.Equal(5, AttendanceScheduleCalculator.CalculateEarlyLeaveMinutes(scheduledEnd, checkOut, 3));
    }

    [Fact]
    public void CalculatesWorkedMinutesAfterUnpaidBreak()
    {
        var checkIn = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 17, 17, 30, 0, DateTimeKind.Utc);

        Assert.Equal(480, AttendanceScheduleCalculator.CalculateWorkedMinutes(checkIn, checkOut, 30));
    }

    [Theory]
    [InlineData(0, 0, "present")]
    [InlineData(5, 0, "late")]
    [InlineData(0, 8, "early_leave")]
    [InlineData(5, 8, "late_and_early")]
    public void ResolvesAttendanceStatus(int late, int early, string expected)
    {
        Assert.Equal(expected, AttendanceScheduleCalculator.ResolveAttendanceStatus(late, early));
    }

    [Fact]
    public void HandlesSpringForwardInvalidLocalShiftBoundary()
    {
        if (!TryGetNewYorkTimeZone(out var zone)) return;

        var window = AttendanceScheduleCalculator.BuildUtcWindow(
            new DateOnly(2026, 3, 8),
            new TimeOnly(2, 30),
            new TimeOnly(3, 30),
            zone);

        Assert.Equal(new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2026, 3, 8, 7, 30, 0, DateTimeKind.Utc), window.EndUtc);
    }

    [Fact]
    public void HandlesFallBackAmbiguousLocalShiftBoundaries()
    {
        if (!TryGetNewYorkTimeZone(out var zone)) return;

        var window = AttendanceScheduleCalculator.BuildUtcWindow(
            new DateOnly(2026, 11, 1),
            new TimeOnly(1, 30),
            new TimeOnly(1, 45),
            zone);

        Assert.Equal(new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 1, 6, 45, 0, DateTimeKind.Utc), window.EndUtc);
    }

    private static bool TryGetNewYorkTimeZone(out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
