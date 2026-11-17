namespace TaskTune.API.Support;

public static class HRWorkflowPolicy
{
    private static readonly string[] ApplicantStages = ["applied", "screening", "interview", "offer", "hired", "rejected"];

    public static bool IsValidApplicantTransition(string current, string next)
    {
        var from = current.Trim().ToLowerInvariant();
        var to = next.Trim().ToLowerInvariant();
        if (!ApplicantStages.Contains(from) || !ApplicantStages.Contains(to) || from == to) return false;
        if (from is "hired" or "rejected") return false;
        if (to == "rejected") return true;
        return Array.IndexOf(ApplicantStages, to) == Array.IndexOf(ApplicantStages, from) + 1;
    }

    public static bool IsValidExpense(decimal amount, string currencyCode)
        => amount > 0m && !string.IsNullOrWhiteSpace(currencyCode) &&
           currencyCode.Length == 3 && currencyCode.All(char.IsAsciiLetter);

    public static bool IsValidGoalProgress(decimal currentValue, decimal targetValue)
        => currentValue >= 0m && targetValue > 0m;

    public static bool IsValidGoalPeriod(DateOnly startDate, DateOnly endDate)
        => endDate >= startDate;

    public static bool IsValidDecision(string decision)
        => decision.Equals("approved", StringComparison.OrdinalIgnoreCase) ||
           decision.Equals("rejected", StringComparison.OrdinalIgnoreCase);
}
