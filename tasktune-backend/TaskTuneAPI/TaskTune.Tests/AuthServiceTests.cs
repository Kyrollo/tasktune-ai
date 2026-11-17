using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using TaskTune.Application.DTOs.Auth;
using TaskTune.Infrastructure.Models;
using TaskTune.Infrastructure.Services;
using Xunit;

namespace TaskTune.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task Login_RejectsLegacyPlaintextPassword()
    {
        await using var context = CreateContext();
        context.Users.Add(CreateUser("legacy-password-value"));
        await context.SaveChangesAsync();

        var service = new AuthService(context, CreateConfiguration());
        var result = await service.LoginAsync(
            new LoginRequestDto("employee@tasktune.test", "legacy-password-value"));

        Assert.Null(result);
    }

    [Fact]
    public async System.Threading.Tasks.Task Login_CreatesSessionFamily_RefreshRotatesAndReplayRevokesFamily()
    {
        await using var context = CreateContext();
        context.Users.Add(CreateUser(BCrypt.Net.BCrypt.HashPassword("Correct-Horse-Battery-42!")));
        await context.SaveChangesAsync();

        var service = new AuthService(context, CreateConfiguration());
        var login = await service.LoginAsync(
            new LoginRequestDto("employee@tasktune.test", "Correct-Horse-Battery-42!"));

        Assert.NotNull(login);
        var firstSessionId = ReadSessionId(login.AccessToken);
        var storedAfterLogin = await context.UserSessions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(firstSessionId, storedAfterLogin.SessionId);
        Assert.Matches("^[A-F0-9]{64}$", storedAfterLogin.TokenHash);
        Assert.Null(storedAfterLogin.ConsumedAt);
        Assert.Null((await context.Users.IgnoreQueryFilters().SingleAsync()).RefreshToken);
        Assert.InRange((login.ExpiresAt - DateTime.UtcNow).TotalMinutes, 18, 20);

        var refreshed = await service.RefreshTokenAsync(login.RefreshToken);
        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        var sessionsAfterRefresh = await context.UserSessions.IgnoreQueryFilters()
            .OrderBy(s => s.CreatedAt).ToListAsync();
        Assert.Equal(2, sessionsAfterRefresh.Count);
        Assert.Equal(sessionsAfterRefresh[0].FamilyId, sessionsAfterRefresh[1].FamilyId);
        Assert.NotNull(sessionsAfterRefresh[0].ConsumedAt);
        Assert.Equal(sessionsAfterRefresh[1].SessionId, sessionsAfterRefresh[0].ReplacedBySessionId);
        Assert.Null(sessionsAfterRefresh[1].ConsumedAt);
        Assert.Equal(ReadSessionId(refreshed.AccessToken), sessionsAfterRefresh[1].SessionId);

        // Reusing an already-consumed refresh token is treated as theft/replay.
        Assert.Null(await service.RefreshTokenAsync(login.RefreshToken));
        var familyAfterReplay = await context.UserSessions.IgnoreQueryFilters().ToListAsync();
        Assert.All(familyAfterReplay, session => Assert.NotNull(session.RevokedAt));
        Assert.Null(await service.RefreshTokenAsync(refreshed.RefreshToken));
    }

    [Fact]
    public async System.Threading.Tasks.Task Logout_RevokesOnlyTheCurrentSessionFamily()
    {
        await using var context = CreateContext();
        context.Users.Add(CreateUser(BCrypt.Net.BCrypt.HashPassword("Correct-Horse-Battery-42!")));
        await context.SaveChangesAsync();

        var service = new AuthService(context, CreateConfiguration());
        var firstLogin = await service.LoginAsync(
            new LoginRequestDto("employee@tasktune.test", "Correct-Horse-Battery-42!"));
        var secondLogin = await service.LoginAsync(
            new LoginRequestDto("employee@tasktune.test", "Correct-Horse-Battery-42!"));

        Assert.NotNull(firstLogin);
        Assert.NotNull(secondLogin);
        var firstFamily = (await context.UserSessions.IgnoreQueryFilters()
            .SingleAsync(s => s.SessionId == ReadSessionId(firstLogin.AccessToken))).FamilyId;
        var secondFamily = (await context.UserSessions.IgnoreQueryFilters()
            .SingleAsync(s => s.SessionId == ReadSessionId(secondLogin.AccessToken))).FamilyId;

        Assert.True(await service.LogoutAsync(1, ReadSessionId(firstLogin.AccessToken)));
        var sessions = await context.UserSessions.IgnoreQueryFilters().ToListAsync();
        Assert.All(sessions.Where(s => s.FamilyId == firstFamily), s => Assert.NotNull(s.RevokedAt));
        Assert.All(sessions.Where(s => s.FamilyId == secondFamily), s => Assert.Null(s.RevokedAt));
    }

    private static Guid ReadSessionId(string accessToken)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return Guid.Parse(jwt.Claims.Single(c => c.Type == "sid").Value);
    }

    private static TaskTuneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TaskTuneDbContext(options);
    }

    private static User CreateUser(string passwordHash)
    {
        var now = DateTime.UtcNow;
        return new User
        {
            CompanyId = 1,
            Email = "employee@tasktune.test",
            PasswordHash = passwordHash,
            FirstName = "Test",
            LastName = "Employee",
            FullName = "Test Employee",
            Role = 0,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IConfiguration CreateConfiguration()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = key,
                ["JwtSettings:Issuer"] = "TaskTuneTests",
                ["JwtSettings:Audience"] = "TaskTuneTestsClient",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "20",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7"
            })
            .Build();
    }
}
