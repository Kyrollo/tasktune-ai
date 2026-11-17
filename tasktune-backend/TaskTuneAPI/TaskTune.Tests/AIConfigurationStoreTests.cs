using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskTune.API.Support;
using TaskTune.Infrastructure.Models;
using Xunit;

namespace TaskTune.Tests;

public sealed class AIConfigurationStoreTests
{
    [Fact]
    public async System.Threading.Tasks.Task ProviderKeys_AreEncryptedAtRestAndNeverReturnedToClientOrAudit()
    {
        const int companyId = 41;
        const string providerKey = "provider-secret-for-test-only";
        var options = new DbContextOptionsBuilder<TaskTuneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var configuration = CreateConfiguration();
        await using var db = new TaskTuneDbContext(options, new FixedTenantContext(companyId));

        db.Companies.Add(new Company
        {
            CompanyId = companyId,
            CompanyName = "Secret Test Tenant",
            SettingsJson = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var config = TaskTuneAIConfiguration.CreateDefault(companyId);
        config.GetAgent("chat_assistant").ApiKey = providerKey;

        await AIConfigurationStore.SaveAsync(db, companyId, config, configuration);

        var storedSettings = db.Companies.Local.Single().SettingsJson;
        Assert.Contains("enc:v1:", storedSettings);
        Assert.DoesNotContain(providerKey, storedSettings);

        var loaded = await AIConfigurationStore.LoadAsync(db, companyId, configuration);
        Assert.Equal(providerKey, loaded.GetAgent("chat_assistant").ApiKey);

        var clientJson = JsonSerializer.Serialize(AIConfigurationStore.ToClientResponse(loaded));
        var auditJson = JsonSerializer.Serialize(AIConfigurationStore.ToAuditSnapshot(loaded));
        Assert.Contains("\"hasApiKey\":true", clientJson);
        Assert.DoesNotContain(providerKey, clientJson);
        Assert.DoesNotContain(providerKey, auditJson);
        Assert.DoesNotContain("apiKey", clientJson);

        loaded.GetAgent("chat_assistant").ApiKey = null;
        loaded.Enabled = false;
        await AIConfigurationStore.SaveAsync(db, companyId, loaded, configuration);

        var reloaded = await AIConfigurationStore.LoadAsync(db, companyId, configuration);
        Assert.Equal(providerKey, reloaded.GetAgent("chat_assistant").ApiKey);
        Assert.False(reloaded.Enabled);
    }

    private static IConfiguration CreateConfiguration()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISecrets:EncryptionKey"] = key
            })
            .Build();
    }

    private sealed class FixedTenantContext(int? companyId) : ITenantContext
    {
        public int? CompanyId { get; } = companyId;
    }
}
