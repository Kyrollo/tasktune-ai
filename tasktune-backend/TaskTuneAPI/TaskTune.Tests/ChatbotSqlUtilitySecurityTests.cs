using TaskTune.API.Support;
using Xunit;

namespace TaskTune.Tests;

public sealed class ChatbotSqlUtilitySecurityTests
{
    [Fact]
    public void AllowsSingleSelectFromApprovedTaskView()
    {
        const string sql = "SELECT TOP 10 TaskId, TaskName FROM dbo.vw_ChatbotTaskFull WHERE CompanyId = @companyId";
        Assert.True(ChatbotSqlUtility.IsQueryAllowed(sql, "Manager", out var error));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void AllowsSingleSelectFromApprovedEvaluationView()
    {
        const string sql = "SELECT EvaluationId, OverallScore FROM dbo.vw_ChatbotEvaluationView WHERE CompanyId = @companyId";
        Assert.True(ChatbotSqlUtility.IsQueryAllowed(sql, "Manager", out _));
    }

    [Fact]
    public void RejectsDirectTableAccess()
    {
        const string sql = "SELECT Email FROM dbo.Users";
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(sql, "Admin", out _));
    }

    [Fact]
    public void RejectsJoinEvenWhenOneSourceIsApproved()
    {
        const string sql = "SELECT v.TaskId FROM dbo.vw_ChatbotTaskFull v JOIN dbo.Users u ON u.UserId = v.AssignedUserId";
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(sql, "Admin", out _));
    }

    [Fact]
    public void RejectsNestedSelect()
    {
        const string sql = "SELECT TaskId FROM dbo.vw_ChatbotTaskFull WHERE AssignedUserId IN (SELECT UserId FROM dbo.Users)";
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(sql, "Admin", out _));
    }

    [Fact]
    public void RejectsUnionAndMultipleStatements()
    {
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(
            "SELECT TaskId FROM dbo.vw_ChatbotTaskFull UNION SELECT UserId FROM dbo.Users", "Admin", out _));
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(
            "SELECT TaskId FROM dbo.vw_ChatbotTaskFull; DROP TABLE dbo.Users", "Admin", out _));
    }

    [Fact]
    public void RejectsSqlComments()
    {
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(
            "SELECT TaskId FROM dbo.vw_ChatbotTaskFull -- bypass", "Admin", out _));
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(
            "SELECT TaskId FROM dbo.vw_ChatbotTaskFull /* comment */", "Admin", out _));
    }

    [Fact]
    public void HrCannotQueryTaskDetailView()
    {
        const string sql = "SELECT TaskId, TaskName FROM dbo.vw_ChatbotTaskFull";
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(sql, "HR", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("DROP TABLE dbo.Users")]
    [InlineData("WITH cte AS (SELECT TaskId FROM dbo.vw_ChatbotTaskFull) SELECT TaskId FROM cte")]
    [InlineData("SELECT * FROM sys.tables")]
    [InlineData("SELECT * FROM dbo.vw_ChatbotTaskFull, dbo.Users")]
    public void RejectsInvalidOrOutOfPolicySql(string sql)
    {
        Assert.False(ChatbotSqlUtility.IsQueryAllowed(sql, "Manager", out _));
    }
}
