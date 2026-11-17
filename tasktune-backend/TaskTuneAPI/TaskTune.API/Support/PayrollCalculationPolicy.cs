namespace TaskTune.API.Support;

public static class PayrollCalculationPolicy
{
    public static decimal Gross(decimal baseSalary, decimal allowances, decimal bonus)
        => baseSalary + allowances + bonus;

    public static decimal Net(
        decimal baseSalary,
        decimal allowances,
        decimal bonus,
        decimal incomeTax,
        decimal socialInsurance,
        decimal otherDeductions)
        => Gross(baseSalary, allowances, bonus) - incomeTax - socialInsurance - otherDeductions;

    public static bool IsValidPayComponents(
        decimal baseSalary,
        decimal allowances,
        decimal bonus,
        decimal incomeTax,
        decimal socialInsurance,
        decimal otherDeductions)
        => new[] { baseSalary, allowances, bonus, incomeTax, socialInsurance, otherDeductions }.All(x => x >= 0m) &&
           Net(baseSalary, allowances, bonus, incomeTax, socialInsurance, otherDeductions) >= 0m;

    public static bool IsValidCurrency(string? currency)
        => currency is { Length: 3 } && currency.All(char.IsAsciiLetter);
}
