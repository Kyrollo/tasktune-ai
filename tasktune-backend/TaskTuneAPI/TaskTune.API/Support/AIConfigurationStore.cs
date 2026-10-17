using System.Text.Json;
using System.Text.Json.Nodes;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public class TaskTuneAIConfiguration
{
    public int CompanyId { get; set; }
    public bool Enabled { get; set; } = true;
    public AIFeaturesConfig Features { get; set; } = new();
    public Dictionary<string, AIAgentConfig> Agents { get; set; } = BuildDefaultAgents();

    public AIAgentConfig GetAgent(string key)
    {
        if (Agents.TryGetValue(key, out var config))
        {
            config.Key ??= key;
            return config;
        }

        var defaults = BuildDefaultAgents();
        var fallback = defaults.GetValueOrDefault(key) ?? new AIAgentConfig { Key = key, Name = key };
        Agents[key] = fallback;
        return fallback;
    }

    public static TaskTuneAIConfiguration CreateDefault(int companyId)
    {
        return new TaskTuneAIConfiguration
        {
            CompanyId = companyId,
            Enabled = true,
            Features = new AIFeaturesConfig
            {
                TaskGuidance = true,
                AutoEvaluation = true,
                RiskDetection = true,
                PerformanceInsights = true,
                SmartSuggestions = true,
                ChatAssistant = true,
                ReportGeneration = true,
                EmailAssistant = true
            },
            Agents = BuildDefaultAgents()
        };
    }

    public static Dictionary<string, AIAgentConfig> BuildDefaultAgents() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["chat_assistant"] = new AIAgentConfig
        {
            Key = "chat_assistant",
            Name = "TaskTune AI Assistant",
            Provider = "groq",
            Model = "llama-3.3-70b-versatile",
            BaseUrl = "http://localhost:8001",
            Endpoint = "/api/chat",
            Temperature = 0.2,
            MaxTokens = 1024,
            Enabled = true
        },
        ["task_guidance"] = new AIAgentConfig
        {
            Key = "task_guidance",
            Name = "Task Guidance",
            Provider = "groq",
            Model = "llama-3.3-70b-versatile",
            BaseUrl = "http://localhost:5050",
            Endpoint = "/api/guidance",
            Temperature = 0.4,
            MaxTokens = 2048,
            Enabled = true
        },
        ["progress_tracking"] = new AIAgentConfig
        {
            Key = "progress_tracking",
            Name = "Progress Tracking",
            Provider = "groq",
            Model = "llama-3.3-70b-versatile",
            BaseUrl = "http://localhost:8003",
            Endpoint = "/api/ai/progress-tracking",
            Temperature = 0.25,
            MaxTokens = 2048,
            Enabled = true
        },
        ["report_generation"] = new AIAgentConfig
        {
            Key = "report_generation",
            Name = "Report Generation",
            Provider = "groq",
            Model = "llama-3.3-70b-versatile",
            BaseUrl = "http://localhost:8004",
            Endpoint = "/api/report/generate",
            Temperature = 0.2,
            MaxTokens = 2000,
            Enabled = true
        },
        ["email_assistant"] = new AIAgentConfig
        {
            Key = "email_assistant",
            Name = "Email Assistant",
            Provider = "groq",
            Model = "llama-3.3-70b-versatile",
            BaseUrl = "http://localhost:8005",
            Endpoint = "/api/email/recommendation",
            Temperature = 0.3,
            MaxTokens = 1200,
            Enabled = true
        }
    };
}

public class AIFeaturesConfig
{
    public bool TaskGuidance { get; set; } = true;
    public bool AutoEvaluation { get; set; } = true;
    public bool RiskDetection { get; set; } = true;
    public bool PerformanceInsights { get; set; } = true;
    public bool SmartSuggestions { get; set; } = true;
    public bool ChatAssistant { get; set; } = true;
    public bool ReportGeneration { get; set; } = true;
    public bool EmailAssistant { get; set; } = true;
}

public class AIAgentConfig
{
    public string? Key { get; set; }
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "groq";
    public string Model { get; set; } = "llama-3.3-70b-versatile";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 1024;
    public bool Enabled { get; set; } = true;
}

public static class AIConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task<TaskTuneAIConfiguration> LoadAsync(TaskTuneDbContext db, int companyId)
    {
        var company = await db.Companies.FindAsync(companyId);
        if (company == null)
        {
            return TaskTuneAIConfiguration.CreateDefault(companyId);
        }

        var config = TryParse(company.SettingsJson, companyId) ?? TaskTuneAIConfiguration.CreateDefault(companyId);
        Normalize(config, companyId);
        return config;
    }

    public static async Task<TaskTuneAIConfiguration> SaveAsync(TaskTuneDbContext db, int companyId, TaskTuneAIConfiguration config)
    {
        var company = await db.Companies.FindAsync(companyId);
        if (company == null)
        {
            throw new InvalidOperationException($"Company {companyId} was not found.");
        }

        Normalize(config, companyId);

        JsonObject root;
        if (string.IsNullOrWhiteSpace(company.SettingsJson))
        {
            root = [];
        }
        else
        {
            try
            {
                root = JsonNode.Parse(company.SettingsJson)?.AsObject() ?? [];
            }
            catch
            {
                root = [];
            }
        }

        root["aiConfig"] = JsonSerializer.SerializeToNode(config, JsonOptions);
        company.SettingsJson = root.ToJsonString(JsonOptions);

        await db.SaveChangesAsync();
        return config;
    }

    public static object ToClientResponse(TaskTuneAIConfiguration config)
    {
        return new
        {
            config.CompanyId,
            config.Enabled,
            features = config.Features,
            agents = config.Agents
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new
                {
                    key = kvp.Key,
                    kvp.Value.Name,
                    kvp.Value.Provider,
                    kvp.Value.Model,
                    kvp.Value.ApiKey,
                    kvp.Value.BaseUrl,
                    kvp.Value.Endpoint,
                    kvp.Value.Temperature,
                    kvp.Value.MaxTokens,
                    kvp.Value.Enabled
                })
        };
    }

    private static TaskTuneAIConfiguration? TryParse(string? settingsJson, int companyId)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return null;
        }

        try
        {
            var root = JsonNode.Parse(settingsJson);
            var aiNode = root?["aiConfig"] ?? root;
            var config = aiNode?.Deserialize<TaskTuneAIConfiguration>(JsonOptions);
            if (config == null)
            {
                return null;
            }

            Normalize(config, companyId);
            return config;
        }
        catch
        {
            return null;
        }
    }

    private static void Normalize(TaskTuneAIConfiguration config, int companyId)
    {
        config.CompanyId = companyId;
        config.Features ??= new AIFeaturesConfig();
        config.Agents ??= [];

        foreach (var (key, agent) in TaskTuneAIConfiguration.BuildDefaultAgents())
        {
            if (!config.Agents.TryGetValue(key, out var existing))
            {
                config.Agents[key] = agent;
                continue;
            }

            existing.Key = key;
            existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? agent.Name : existing.Name;
            existing.Provider = string.IsNullOrWhiteSpace(existing.Provider) ? agent.Provider : existing.Provider;
            existing.Model = string.IsNullOrWhiteSpace(existing.Model) ? agent.Model : existing.Model;
            existing.BaseUrl = string.IsNullOrWhiteSpace(existing.BaseUrl) ? agent.BaseUrl : existing.BaseUrl;
            existing.Endpoint = string.IsNullOrWhiteSpace(existing.Endpoint) ? agent.Endpoint : existing.Endpoint;
            existing.Temperature = existing.Temperature == 0 ? agent.Temperature : existing.Temperature;
            existing.MaxTokens = existing.MaxTokens == 0 ? agent.MaxTokens : existing.MaxTokens;
        }
    }
}
