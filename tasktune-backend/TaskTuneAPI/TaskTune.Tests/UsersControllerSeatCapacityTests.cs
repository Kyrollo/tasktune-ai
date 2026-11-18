using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskTune.API.Controllers;
using TaskTune.API.Support;
using TaskTune.Application.DTOs.Users;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class UsersControllerSeatCapacityTests
{
    [Fact]
    public async System.Threading.Tasks.Task DirectUserCreationRejectsWhenPlanSeatsAreAlreadyReserved()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = new Company { CompanyId = 1, CompanyName = "Seat capacity", SettingsJson = "{}", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var admin = new User
        {
            UserId = 10, CompanyId = 1, Company = company, Email = "admin@tenant.test",
            PasswordHash = "test", FirstName = "Tenant", LastName = "Admin",
            FullName = "Tenant Admin", Role = 5, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var plan = new SubscriptionPlan
        {
            PlanId = 1, Code = "one-seat", Name = "One Seat", IsActive = true,
            MaxUsers = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Companies.Add(company);
        db.Users.Add(admin);
        db.SubscriptionPlans.Add(plan);
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            CompanyId = 1, Company = company, PlanId = 1, Plan = plan,
            Status = "Active", StartsAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var workflow = new AccountSecurityWorkflowService(
            db, new FakeEmailSender(), new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new TenantSeatCapacityService(db));
        var controller = new UsersController(db, workflow, new TenantSeatCapacityService(db));
        var identity = new ClaimsIdentity(
            [new Claim("sub", "10"), new Claim("companyId", "1"), new Claim("role", "Admin")],
            authenticationType: "UnitTest");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = Assert.IsType<ObjectResult>(await controller.CreateUser(
            new CreateUserDto(999, "new@tenant.test", "Correct-Horse-Battery-42!", "New", "User", 0, null, null, null)));
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(1, await db.Users.IgnoreQueryFilters().CountAsync());
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }

    private sealed class FakeEmailSender : ITransactionalEmailSender
    {
        public System.Threading.Tasks.Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.CompletedTask;
    }
}
