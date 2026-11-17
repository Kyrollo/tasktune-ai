namespace TaskTune.Infrastructure.Models;

public partial class AccountSecurityToken
{
    public int AccountSecurityTokenId { get; set; }
    public int CompanyId { get; set; }
    public int? UserId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public byte? InvitedRole { get; set; }
    public int? DepartmentId { get; set; }
    public int? PositionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
