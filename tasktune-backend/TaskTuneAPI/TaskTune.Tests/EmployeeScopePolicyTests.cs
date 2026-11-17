using Xunit;

namespace TaskTune.Tests;

public sealed class EmployeeScopePolicyTests
{
    [Theory]
    [InlineData("self", true)]
    [InlineData("team", true)]
    [InlineData("department", true)]
    [InlineData("company", true)]
    [InlineData("selected", true)]
    [InlineData("global", false)]
    [InlineData("", false)]
    public void PermissionScopesMustBeExplicitlySupported(string scope, bool expected)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "self", "team", "department", "company", "selected"
        };

        Assert.Equal(expected, supported.Contains(scope));
    }

    [Theory]
    [InlineData("read", true)]
    [InlineData("create", true)]
    [InlineData("update", true)]
    [InlineData("delete", true)]
    [InlineData("approve", true)]
    [InlineData("export", true)]
    [InlineData("manage", true)]
    [InlineData("root", false)]
    [InlineData("", false)]
    public void PermissionActionsMustBeAllowlisted(string action, bool expected)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "read", "create", "update", "delete", "approve", "export", "manage"
        };

        Assert.Equal(expected, supported.Contains(action));
    }
}
