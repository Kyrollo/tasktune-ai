#nullable disable
using System;

namespace TaskTune.Infrastructure.Models;

public partial class UserSetting
{
    public int UserSettingId { get; set; }
    public int UserId { get; set; }
    public string SettingsJson { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual User User { get; set; }
}
