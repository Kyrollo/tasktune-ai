using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantQueryFilterMetadataTests
{
    private static readonly Type[] TenantOwnedEntities =
    [
        typeof(Company),
        typeof(User),
        typeof(Department),
        typeof(Position),
        typeof(Project),
        typeof(RolePermission),
        typeof(Report),
        typeof(CommunicationLog),
        typeof(CommunicationApproval),
        typeof(CommunicationOutbox),
        typeof(TaskTune.Infrastructure.Models.Task),
        typeof(Subtask),
        typeof(TaskComment),
        typeof(TaskAccessControl),
        typeof(TaskDependency),
        typeof(TaskProgress),
        typeof(TaskAttachment),
        typeof(Evaluation),
        typeof(Bug),
        typeof(Notification),
        typeof(AuditLog),
        typeof(AIActionsLog),
        typeof(ChatThread),
        typeof(ChatMessage),
        typeof(UserSetting),
        typeof(UserSession),
        typeof(AccountSecurityToken)
    ];

    [Fact]
    public void EveryTenantOwnedEntityHasAGlobalQueryFilter()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        using var db = new TaskTuneDbContext(options, new FixedTenantContext(null));
        var missing = TenantOwnedEntities
            .Where(type => db.Model.FindEntityType(type)?.GetQueryFilter() is null)
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(missing);
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
