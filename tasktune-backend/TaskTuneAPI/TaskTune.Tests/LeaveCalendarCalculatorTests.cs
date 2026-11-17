using TaskTune.API.Support;
using Xunit;

namespace TaskTune.Tests;

public sealed class LeaveCalendarCalculatorTests
{
    [Fact]
    public void SplitsWorkingDaysAcrossCalendarYears()
    {
        var result = LeaveCalendarCalculator.CountWorkingDaysByYear(
            new DateOnly(2026, 12, 31),
            new DateOnly(2027, 1, 4));

        Assert.Equal(2m, result[2026]);
        Assert.Equal(1m, result[2027]);
        Assert.Equal(3m, result.Values.Sum());
    }

    [Fact]
    public void ExcludesConfiguredCompanyHolidays()
    {
        var holidays = new[] { new DateOnly(2026, 11, 18) };
        var result = LeaveCalendarCalculator.CountWorkingDaysByYear(
            new DateOnly(2026, 11, 16),
            new DateOnly(2026, 11, 20),
            holidays);

        Assert.Equal(5m, result[2026]);
    }

    [Fact]
    public void AllowsCompanySpecificWeekendDays()
    {
        var result = LeaveCalendarCalculator.CountWorkingDaysByYear(
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 19),
            weekendDays: new[] { DayOfWeek.Friday, DayOfWeek.Saturday });

        Assert.Equal(4m, result[2026]);
    }

    [Fact]
    public void RejectsEndDateBeforeStartDate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LeaveCalendarCalculator.CountWorkingDaysByYear(
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 19)));
    }
}
