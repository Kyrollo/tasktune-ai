namespace TaskTune.Infrastructure.Models;

public partial class UserPermissionOverride
{
    public int PermissionOverrideId { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool CanPerform { get; set; }
    public string Scope { get; set; } = "self";
    public int? ScopeResourceId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
    public int GrantedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
