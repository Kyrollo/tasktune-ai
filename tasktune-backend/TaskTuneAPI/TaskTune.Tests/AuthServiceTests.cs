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
    public async Task Login_RejectsLegacyPlaintextPassword()
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
    public async Task Login_StoresOnlyRefreshTokenDigest_AndRefreshRotatesToken()
    {
        await using var context = CreateContext();
        context.Users.Add(CreateUser(BCrypt.Net.BCrypt.HashPassword("Correct-Horse-Battery-42!")));
        await context.SaveChangesAsync();

        var service = new AuthService(context, CreateConfiguration());
        var login = await service.LoginAsync(
            new LoginRequestDto("employee@tasktune.test", "Correct-Horse-Battery-42!"));

        Assert.NotNull(login);
        var storedAfterLogin = await context.Users.SingleAsync();
        Assert.Matches("^[A-F0-9]{64}$", storedAfterLogin.RefreshToken!);
        Assert.NotEqual(login.RefreshToken, storedAfterLogin.RefreshToken);
        Assert.InRange((login.ExpiresAt - DateTime.UtcNow).TotalMinutes, 18, 20);

        var refreshed = await service.RefreshTokenAsync(login.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Null(await service.RefreshTokenAsync(login.RefreshToken));

        var storedAfterRefresh = await context.Users.SingleAsync();
        Assert.Matches("^[A-F0-9]{64}$", storedAfterRefresh.RefreshToken!);
        Assert.NotEqual(refreshed.RefreshToken, storedAfterRefresh.RefreshToken);
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
