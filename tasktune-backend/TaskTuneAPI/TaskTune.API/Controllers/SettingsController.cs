using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTune.Infrastructure.Models;

namespace TaskTune.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : BaseApiController
{
    private readonly TaskTuneDbContext _db;

    private const string DefaultSettingsJson =
        """{"theme":"dark","language":"en","notifications":{"email":true,"push":true,"digest":false}}""";

    public SettingsController(TaskTuneDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var setting = await _db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId.Value);

        return Ok(new
        {
            settingsJson = setting?.SettingsJson ?? DefaultSettingsJson,
            updatedAt = setting?.UpdatedAt
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpsertSettings([FromBody] UpsertSettingsDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var setting = await _db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId.Value);

        if (setting == null)
        {
            setting = new UserSetting
            {
                UserId = userId.Value,
                SettingsJson = dto.SettingsJson,
                UpdatedAt = DateTime.UtcNow
            };
            _db.UserSettings.Add(setting);
        }
        else
        {
            setting.SettingsJson = dto.SettingsJson;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            setting.SettingsJson,
            setting.UpdatedAt
        });
    }
}

public record UpsertSettingsDto(string SettingsJson);
