using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Support;

public sealed class CompanyWorkCalendarSettings
{
    public List<int> WeekendDays { get; set; } = [0, 6];
}

public static class WorkCalendarSettingsStore
{
    public static async Task<CompanyWorkCalendarSettings> LoadAsync(TaskTuneDbContext db, int companyId)
    {
        var json = await db.Companies.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.SettingsJson)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(json))
            return new CompanyWorkCalendarSettings();

        try
        {
            var root = JsonNode.Parse(json);
            var node = root?["workCalendar"];
            var weekendDays = node?["weekendDays"]?.AsArray()
                .Select(x => x?.GetValue<int>())
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            if (weekendDays == null || weekendDays.Count == 0 ||
                weekendDays.Any(x => x is < 0 or > 6) ||
                weekendDays.Distinct().Count() != weekendDays.Count ||
                weekendDays.Count > 3)
                return new CompanyWorkCalendarSettings();

            return new CompanyWorkCalendarSettings { WeekendDays = weekendDays.OrderBy(x => x).ToList() };
        }
        catch
        {
            return new CompanyWorkCalendarSettings();
        }
    }

    public static async Task<CompanyWorkCalendarSettings> SaveAsync(
        TaskTuneDbContext db, int companyId, IReadOnlyCollection<int> weekendDays)
    {
        if (weekendDays.Count is < 1 or > 3 ||
            weekendDays.Any(x => x is < 0 or > 6) ||
            weekendDays.Distinct().Count() != weekendDays.Count)
            throw new ArgumentException("Choose one to three distinct weekend days (0=Sunday through 6=Saturday).", nameof(weekendDays));

        var company = await db.Companies.FirstOrDefaultAsync(x => x.CompanyId == companyId);
        if (company == null)
            throw new InvalidOperationException("Company was not found.");

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
                throw new InvalidOperationException("Company settings JSON is invalid. Repair settings before changing the work calendar.");
            }
        }

        root["workCalendar"] = new JsonObject
        {
            ["weekendDays"] = new JsonArray(weekendDays.OrderBy(x => x).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray())
        };
        company.SettingsJson = root.ToJsonString();
        company.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new CompanyWorkCalendarSettings { WeekendDays = weekendDays.OrderBy(x => x).ToList() };
    }
}
