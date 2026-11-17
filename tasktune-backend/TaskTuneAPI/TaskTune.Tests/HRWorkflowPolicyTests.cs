using TaskTune.API.Support;
using Xunit;

namespace TaskTune.Tests;

public sealed class HRWorkflowPolicyTests
{
    [Theory]
    [InlineData("applied", "screening", true)]
    [InlineData("screening", "interview", true)]
    [InlineData("interview", "offer", true)]
    [InlineData("offer", "hired", true)]
    [InlineData("applied", "rejected", true)]
    [InlineData("screening", "applied", false)]
    [InlineData("applied", "offer", false)]
    [InlineData("hired", "rejected", false)]
    [InlineData("rejected", "screening", false)]
    [InlineData("applied", "applied", false)]
    public void ApplicantStageTransitionsAreControlled(string from, string to, bool expected)
        => Assert.Equal(expected, HRWorkflowPolicy.IsValidApplicantTransition(from, to));

    [Theory]
    [InlineData(1, "USD", true)]
    [InlineData(250.50, "EGP", true)]
    [InlineData(0, "USD", false)]
    [InlineData(-1, "USD", false)]
    [InlineData(1, "US", false)]
    [InlineData(1, "USDD", false)]
    public void ExpenseAmountAndCurrencyAreValidated(decimal amount, string currency, bool expected)
        => Assert.Equal(expected, HRWorkflowPolicy.IsValidExpense(amount, currency));

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(-0.01, 1, false)]
    [InlineData(0, 0, false)]
    public void GoalProgressMustBeNonNegativeAgainstPositiveTarget(decimal current, decimal target, bool expected)
        => Assert.Equal(expected, HRWorkflowPolicy.IsValidGoalProgress(current, target));

    [Theory]
    [InlineData("2026-01-01", "2026-01-01", true)]
    [InlineData("2026-01-01", "2026-01-02", true)]
    [InlineData("2026-01-02", "2026-01-01", false)]
    public void GoalPeriodMustBeOrdered(string start, string end, bool expected)
        => Assert.Equal(expected, HRWorkflowPolicy.IsValidGoalPeriod(DateOnly.Parse(start), DateOnly.Parse(end)));
}
