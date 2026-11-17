using TaskTune.API.Support;
using Xunit;

namespace TaskTune.Tests;

public sealed class PayrollCalculationPolicyTests
{
    [Fact]
    public void GrossAndNetPayUseTransparentComponentArithmetic()
    {
        Assert.Equal(15000m, PayrollCalculationPolicy.Gross(12000m, 2000m, 1000m));
        Assert.Equal(12500m, PayrollCalculationPolicy.Net(12000m, 2000m, 1000m, 1000m, 500m, 1000m));
    }

    [Theory]
    [InlineData(1000, 100, 0, 100, 100, 100, true)]
    [InlineData(1000, 0, 0, 500, 500, 1, false)]
    [InlineData(-1, 0, 0, 0, 0, 0, false)]
    [InlineData(1000, -1, 0, 0, 0, 0, false)]
    public void PayComponentsMustBeNonNegativeAndNetCannotBeNegative(decimal baseSalary, decimal allowance, decimal bonus, decimal tax, decimal insurance, decimal other, bool expected)
        => Assert.Equal(expected, PayrollCalculationPolicy.IsValidPayComponents(baseSalary, allowance, bonus, tax, insurance, other));

    [Theory]
    [InlineData("EGP", true)]
    [InlineData("usd", true)]
    [InlineData("US", false)]
    [InlineData("EURO", false)]
    [InlineData("", false)]
    public void CurrencyMustBeThreeLetters(string currency, bool expected)
        => Assert.Equal(expected, PayrollCalculationPolicy.IsValidCurrency(currency));
}
