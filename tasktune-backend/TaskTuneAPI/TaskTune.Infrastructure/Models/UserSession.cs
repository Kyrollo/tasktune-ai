namespace TaskTune.Infrastructure.Models;

public partial class UserSession
{
    public Guid SessionId { get; set; }
    public int UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public virtual User User { get; set; } = null!;
}
