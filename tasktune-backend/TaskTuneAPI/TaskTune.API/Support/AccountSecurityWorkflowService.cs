using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskTune.Infrastructure.Models;
using Task = System.Threading.Tasks.Task;

namespace TaskTune.API.Support;

public sealed record AccountWorkflowResult(bool Success, string? Error = null, int? UserId = null);

public sealed class AccountSecurityWorkflowService
{
    private const string InvitationPurpose = "Invitation";
    private const string PasswordResetPurpose = "PasswordReset";
    private readonly TaskTuneDbContext _db;
    private readonly ITransactionalEmailSender _email;
    private readonly IConfiguration _configuration;

    public AccountSecurityWorkflowService(
        TaskTuneDbContext db,
        ITransactionalEmailSender email,
        IConfiguration configuration)
    {
        _db = db;
        _email = email;
        _configuration = configuration;
    }

    public async Task<AccountWorkflowResult> CreateInvitationAsync(
        int companyId,
        int actorId,
        string email,
        byte role,
        int? departmentId,
        int? positionId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null) return new(false, "A valid email address is required.");
        if (role > 5) return new(false, "Invalid role.");
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
            return new(false, "Email already exists.");

        if (departmentId.HasValue &&
            !await _db.Departments.AnyAsync(d => d.DepartmentId == departmentId && d.CompanyId == companyId, cancellationToken))
            return new(false, "Department must belong to the inviting company.");

        if (positionId.HasValue)
        {
            var position = await _db.Positions.FirstOrDefaultAsync(
                p => p.PositionId == positionId && p.CompanyId == companyId, cancellationToken);
            if (position == null || (departmentId.HasValue && position.DepartmentId != departmentId))
                return new(false, "Position must belong to the company and selected department.");
        }

        var now = DateTime.UtcNow;
        var staleInvites = await _db.AccountSecurityTokens.Where(t =>
            t.CompanyId == companyId && t.Purpose == InvitationPurpose &&
            t.Email == normalizedEmail && t.ConsumedAt == null).ToListAsync(cancellationToken);
        foreach (var stale in staleInvites) stale.ConsumedAt = now;

        var rawToken = CreateOpaqueToken();
        var invite = new AccountSecurityToken
        {
            CompanyId = companyId,
            CreatedByUserId = actorId,
            Email = normalizedEmail,
            Purpose = InvitationPurpose,
            TokenHash = HashToken(rawToken),
            InvitedRole = role,
            DepartmentId = departmentId,
            PositionId = positionId,
            CreatedAt = now,
            ExpiresAt = now.AddHours(48)
        };
        _db.AccountSecurityTokens.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var link = BuildFrontendLink("signup", "invite", rawToken);
            await _email.SendAsync(
                normalizedEmail,
                "Your TaskTune AI invitation",
                $"You have been invited to join a TaskTune AI workspace. Open this link to create your account and verify this email address:\n\n{link}\n\nThis invitation expires in 48 hours and can only be used once.",
                cancellationToken);
            return new(true);
        }
        catch
        {
            invite.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return new(false, "Invitation email could not be sent. Verify SMTP settings and retry.");
        }
    }

    public async Task<AccountWorkflowResult> CompleteInvitationAsync(
        string inviteCode,
        string email,
        string firstName,
        string lastName,
        string password,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(inviteCode) || inviteCode.Length > 160)
            return new(false, "The invitation is invalid or expired.");
        if (normalizedEmail == null || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return new(false, "Email and name fields are required.");
        if (password.Length < 12 || password != confirmPassword)
            return new(false, "Passwords must match and contain at least 12 characters.");

        var tokenHash = HashToken(inviteCode);
        var now = DateTime.UtcNow;
        var invitation = await _db.AccountSecurityTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == InvitationPurpose, cancellationToken);
        if (invitation == null || invitation.ConsumedAt.HasValue || invitation.ExpiresAt <= now ||
            !string.Equals(invitation.Email, normalizedEmail, StringComparison.Ordinal))
            return new(false, "The invitation is invalid or expired.");

        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
            return new(false, "The invitation is no longer available.");

        if (invitation.InvitedRole == null || invitation.InvitedRole > 5 ||
            (invitation.DepartmentId.HasValue && !await _db.Departments.IgnoreQueryFilters().AnyAsync(
                d => d.DepartmentId == invitation.DepartmentId && d.CompanyId == invitation.CompanyId, cancellationToken)))
            return new(false, "The invitation contains invalid organization details.");

        if (invitation.PositionId.HasValue)
        {
            var validPosition = await _db.Positions.IgnoreQueryFilters().AnyAsync(p =>
                p.PositionId == invitation.PositionId && p.CompanyId == invitation.CompanyId &&
                (!invitation.DepartmentId.HasValue || p.DepartmentId == invitation.DepartmentId), cancellationToken);
            if (!validPosition) return new(false, "The invitation contains invalid organization details.");
        }

        var user = new User
        {
            CompanyId = invitation.CompanyId,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            FullName = $"{firstName.Trim()} {lastName.Trim()}",
            Role = invitation.InvitedRole.Value,
            DepartmentId = invitation.DepartmentId,
            PositionId = invitation.PositionId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var relational = _db.Database.IsRelational();
        if (relational) await _db.Database.BeginTransactionAsync(cancellationToken);
        invitation.ConsumedAt = now;
        invitation.UserId = null;
        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            if (relational) await _db.Database.CommitTransactionAsync(cancellationToken);
            return new(true, UserId: user.UserId);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (relational) await _db.Database.RollbackTransactionAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return new(false, "The invitation has already been used.");
        }
        catch
        {
            if (relational) await _db.Database.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null) return;

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive, cancellationToken);
        if (user == null) return;

        var now = DateTime.UtcNow;
        var priorTokens = await _db.AccountSecurityTokens.IgnoreQueryFilters().Where(t =>
            t.UserId == user.UserId && t.Purpose == PasswordResetPurpose && t.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var prior in priorTokens) prior.ConsumedAt = now;

        var rawToken = CreateOpaqueToken();
        var reset = new AccountSecurityToken
        {
            CompanyId = user.CompanyId,
            UserId = user.UserId,
            Email = normalizedEmail,
            Purpose = PasswordResetPurpose,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30)
        };
        _db.AccountSecurityTokens.Add(reset);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var link = BuildFrontendLink("forgot-password", "token", rawToken);
            await _email.SendAsync(
                normalizedEmail,
                "Reset your TaskTune AI password",
                $"Use this one-time link to reset your password:\n\n{link}\n\nThe link expires in 30 minutes. If you did not request this, ignore this email.",
                cancellationToken);
        }
        catch
        {
            reset.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            // Deliberately do not expose delivery or account-existence information to callers.
        }
    }

    public async Task<AccountWorkflowResult> ResetPasswordAsync(
        string token,
        string password,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 160 ||
            password.Length < 12 || password != confirmPassword)
            return new(false, "The reset link is invalid or expired, or the password does not meet requirements.");

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(token);
        var reset = await _db.AccountSecurityTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == PasswordResetPurpose, cancellationToken);
        if (reset == null || reset.ConsumedAt.HasValue || reset.ExpiresAt <= now || !reset.UserId.HasValue)
            return new(false, "The reset link is invalid or expired.");

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == reset.UserId.Value && u.CompanyId == reset.CompanyId && u.IsActive, cancellationToken);
        if (user == null || !string.Equals(user.Email, reset.Email, StringComparison.Ordinal))
            return new(false, "The reset link is invalid or expired.");

        var relational = _db.Database.IsRelational();
        if (relational) await _db.Database.BeginTransactionAsync(cancellationToken);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.UpdatedAt = now;
        reset.ConsumedAt = now;

        var sessions = await _db.UserSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == user.UserId && s.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var session in sessions) session.RevokedAt = now;

        var outstanding = await _db.AccountSecurityTokens.IgnoreQueryFilters().Where(t =>
            t.UserId == user.UserId && t.ConsumedAt == null).ToListAsync(cancellationToken);
        foreach (var other in outstanding) other.ConsumedAt = now;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            if (relational) await _db.Database.CommitTransactionAsync(cancellationToken);
            return new(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (relational) await _db.Database.RollbackTransactionAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return new(false, "The reset link has already been used.");
        }
        catch
        {
            if (relational) await _db.Database.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private string BuildFrontendLink(string route, string queryName, string token)
    {
        var baseUrl = _configuration["Web:BaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && !uri.IsLoopback))
            throw new InvalidOperationException("Web:BaseUrl must be an HTTPS frontend URL (HTTP is allowed only for loopback development).");

        return $"{baseUrl}/{route}?{Uri.EscapeDataString(queryName)}={Uri.EscapeDataString(token)}";
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = email.Trim().ToLowerInvariant();
        return normalized.Length <= 255 && System.Net.Mail.MailAddress.TryCreate(normalized, out var parsed) &&
            string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase) ? normalized : null;
    }

    private static string CreateOpaqueToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
