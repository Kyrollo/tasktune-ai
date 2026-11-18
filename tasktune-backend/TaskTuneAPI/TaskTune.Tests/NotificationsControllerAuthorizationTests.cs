using System.Collections;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskTune.API.Controllers;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class NotificationsControllerAuthorizationTests
{
    [Fact]
    public async System.Threading.Tasks.Task ListReturnsOnlyTheAuthenticatedUsersNotifications()
    {
        var (db, ownId, otherId) = await CreateScenarioAsync();
        await using (db)
        {
            var controller = CreateController(db, 10);
            var result = Assert.IsType<OkObjectResult>(await controller.GetNotifications());
            var values = Assert.IsAssignableFrom<IEnumerable>(result.Value);
            var ids = values.Cast<object>()
                .Select(value => (int)value.GetType().GetProperty("NotificationId")!.GetValue(value)!)
                .ToArray();

            Assert.Equal(new[] { ownId }, ids);
            Assert.DoesNotContain(otherId, ids);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task UserCannotMarkOrDeleteAnotherUsersNotification()
    {
        var (db, ownId, otherId) = await CreateScenarioAsync();
        await using (db)
        {
            var controller = CreateController(db, 10);
            Assert.IsType<NotFoundResult>(await controller.MarkRead(otherId));
            Assert.IsType<NotFoundResult>(await controller.DeleteNotification(otherId));

            var own = await db.Notifications.SingleAsync(n => n.NotificationId == ownId);
            var other = await db.Notifications.IgnoreQueryFilters().SingleAsync(n => n.NotificationId == otherId);
            Assert.False(own.IsRead);
            Assert.False(other.IsRead);
            Assert.Equal(2, await db.Notifications.IgnoreQueryFilters().CountAsync());
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task MarkAllReadUpdatesOnlyTheAuthenticatedUsersRows()
    {
        var source = Environment.GetEnvironmentVariable("TASKTUNE_SQLSERVER_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(source),
            "TASKTUNE_SQLSERVER_TEST_CONNECTION must be configured by the SQL Server CI service.");
        var connection = new SqlConnectionStringBuilder(source!)
        {
            InitialCatalog = $"TaskTuneNotificationScope_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseSqlServer(connection.ConnectionString).Options;

        try
        {
            int ownId;
            int otherId;
            await using (var seed = new TaskTuneDbContext(options, new FixedTenantContext(null)))
            {
                await seed.Database.EnsureCreatedAsync();
                var company = CreateCompany(1);
                var user = CreateUser(10, company, "first@tenant.test");
                var otherUser = CreateUser(11, company, "second@tenant.test");
                seed.Companies.Add(company);
                seed.Users.AddRange(user, otherUser);
                await seed.SaveChangesAsync();

                var now = DateTime.UtcNow;
                var own = CreateNotification(user, "Own notification", now);
                var other = CreateNotification(otherUser, "Other notification", now);
                seed.Notifications.AddRange(own, other);
                await seed.SaveChangesAsync();
                ownId = own.NotificationId;
                otherId = other.NotificationId;
            }

            await using var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
            var controller = CreateController(db, 10);
            Assert.IsType<OkObjectResult>(await controller.MarkAllRead());

            var rows = await db.Notifications.IgnoreQueryFilters().ToListAsync();
            Assert.True(rows.Single(n => n.NotificationId == ownId).IsRead);
            Assert.NotNull(rows.Single(n => n.NotificationId == ownId).ReadAt);
            Assert.False(rows.Single(n => n.NotificationId == otherId).IsRead);
            Assert.Null(rows.Single(n => n.NotificationId == otherId).ReadAt);
        }
        finally
        {
            await using var cleanup = new TaskTuneDbContext(options, new FixedTenantContext(null));
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static NotificationsController CreateController(TaskTuneDbContext db, int userId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("companyId", "1"),
            new Claim("role", "Employee")
        ], authenticationType: "UnitTest");
        var controller = new NotificationsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
        return controller;
    }

    private static async System.Threading.Tasks.Task<(TaskTuneDbContext Db, int OwnId, int OtherId)> CreateScenarioAsync()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var db = new TaskTuneDbContext(options, new FixedTenantContext(1));
        var company = CreateCompany(1);
        var user = CreateUser(10, company, "first@tenant.test");
        var otherUser = CreateUser(11, company, "second@tenant.test");
        db.Companies.Add(company);
        db.Users.AddRange(user, otherUser);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var own = CreateNotification(user, "Own notification", now);
        var other = CreateNotification(otherUser, "Other notification", now);
        db.Notifications.AddRange(own, other);
        await db.SaveChangesAsync();
        return (db, own.NotificationId, other.NotificationId);
    }

    private static Company CreateCompany(int id) => new()
    {
        CompanyId = id, CompanyName = "Notification Tenant", SettingsJson = "{}",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static User CreateUser(int id, Company company, string email) => new()
    {
        UserId = id, CompanyId = company.CompanyId, Company = company,
        Email = email, PasswordHash = "test-hash",
        FirstName = "Test", LastName = "User", FullName = "Test User",
        Role = 0, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Notification CreateNotification(User user, string title, DateTime now) => new()
    {
        UserId = user.UserId, User = user, NotificationType = 0,
        Title = title, Message = title, IsRead = false, SentAt = now
    };

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
