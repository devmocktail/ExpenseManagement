using ExpenseManagement.Domain.Common;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// One issued refresh token. The raw token is never persisted — only a SHA-256
/// hash — so a database leak cannot be replayed against the API.
///
/// Tokens rotate: redeeming one issues a successor and stamps
/// <see cref="ReplacedByTokenHash"/>. Presenting an already-rotated token is
/// treated as theft and revokes the whole family (see <see cref="FamilyId"/>).
/// </summary>
public class RefreshToken : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Base64 SHA-256 of the raw token. Unique.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Shared by every token descended from one login. Revoking the family
    /// logs out that device chain without touching the user's other devices.
    /// </summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Coarse client fingerprint for the "active sessions" screen. Never a secret.</summary>
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActiveAt(DateTimeOffset now) => !IsRevoked && !IsExpiredAt(now);
}
