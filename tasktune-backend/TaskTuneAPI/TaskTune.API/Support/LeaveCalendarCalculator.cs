namespace TaskTune.API.Support;

public static class LeaveCalendarCalculator
{
    private static readonly DayOfWeek[] DefaultWeekendDays =
    [
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    public static Dictionary<int, decimal> CountWorkingDaysByYear(
        DateOnly start,
        DateOnly end,
        IReadOnlyCollection<DateOnly>? holidays = null,
        IReadOnlyCollection<DayOfWeek>? weekendDays = null)
    {
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "The end date cannot be earlier than the start date.");

        var holidaySet = holidays?.ToHashSet() ?? [];
        var weekendSet = (weekendDays ?? DefaultWeekendDays).ToHashSet();
        var result = new Dictionary<int, decimal>();

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (weekendSet.Contains(day.DayOfWeek) || holidaySet.Contains(day))
                continue;

            result[day.Year] = result.GetValueOrDefault(day.Year) + 1m;
        }

        return result;
    }
}
