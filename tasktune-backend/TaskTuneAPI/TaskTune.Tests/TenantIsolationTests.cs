using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async System.Threading.Tasks.Task TenantFilters_RestrictCompanyAndRelationshipOwnedRecords()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
        {
            var companyOne = CompanyFor(1);
            var companyTwo = CompanyFor(2);
            var userOne = UserFor(1, companyOne);
            var userTwo = UserFor(2, companyTwo);
            var projectOne = ProjectFor(101, companyOne, userOne);
            var projectTwo = ProjectFor(202, companyTwo, userTwo);
            var taskOne = TaskFor(1001, projectOne, userOne);
            var taskTwo = TaskFor(2002, projectTwo, userTwo);
            seed.AddRange(companyOne, companyTwo, userOne, userTwo, projectOne, projectTwo,
                taskOne, taskTwo, SubtaskFor(11, taskOne), SubtaskFor(22, taskTwo));
            await seed.SaveChangesAsync();
        }

        await using var tenantOne = new TaskTuneDbContext(options, new FixedTenantContext(1));
        Assert.Equal(new[] { 1 }, await tenantOne.Companies.Select(c => c.CompanyId).ToArrayAsync());
        Assert.Equal(new[] { 1 }, await tenantOne.Users.Select(u => u.CompanyId).ToArrayAsync());
        Assert.Equal(new[] { 1 }, await tenantOne.Projects.Select(p => p.CompanyId).ToArrayAsync());
        Assert.Equal(new[] { 1001 }, await tenantOne.Tasks.Select(t => t.TaskId).ToArrayAsync());
        Assert.Equal(new[] { 11 }, await tenantOne.Subtasks.Select(s => s.SubtaskId).ToArrayAsync());
        Assert.Equal(2, await tenantOne.Users.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await tenantOne.Tasks.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task MissingTenantContext_FailsClosed()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
        {
            seed.Users.Add(UserFor(1, CompanyFor(1)));
            await seed.SaveChangesAsync();
        }
        await using var unscoped = new TaskTuneDbContext(options, new FixedTenantContext(null));
        Assert.Empty(await unscoped.Users.ToListAsync());
        Assert.Empty(await unscoped.Companies.ToListAsync());
    }

    private static Company CompanyFor(int id) => new()
    {
        CompanyId = id, CompanyName = $"Company {id}",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User UserFor(int id, Company company) => new()
    {
        UserId = id, CompanyId = company.CompanyId, Company = company,
        Email = $"user{id}@tenant.test", PasswordHash = "test-hash",
        FirstName = $"User{id}", LastName = "Test", FullName = $"User{id} Test",
        Role = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Project ProjectFor(int id, Company company, User manager) => new()
    {
        ProjectId = id, CompanyId = company.CompanyId, Company = company,
        ProjectName = $"Project {id}", ManagerId = manager.UserId, Manager = manager,
        Status = 0, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31),
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static TaskTune.Infrastructure.Models.Task TaskFor(int id, Project project, User user) => new()
    {
        TaskId = id, ProjectId = project.ProjectId, Project = project,
        TaskName = $"Task {id}", Priority = 1, Status = 0,
        AssignedTo = user.UserId, AssignedToNavigation = user,
        CreatedBy = user.UserId, CreatedByNavigation = user,
        StartDate = new DateOnly(2026, 1, 1), DueDate = new DateOnly(2026, 2, 1),
        CompletionPercentage = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Subtask SubtaskFor(int id, TaskTune.Infrastructure.Models.Task task) => new()
    {
        SubtaskId = id, TaskId = task.TaskId, Task = task,
        SubtaskName = $"Subtask {id}", Status = 0, OrderIndex = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
