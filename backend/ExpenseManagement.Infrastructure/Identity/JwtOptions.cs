using System.ComponentModel.DataAnnotations;

namespace ExpenseManagement.Infrastructure.Identity;

/// <summary>
/// JWT configuration, bound from the <c>Jwt</c> section and validated at
/// startup. Validation is deliberately fail-fast: a missing or short signing
/// key must stop the process, not surface later as tokens anyone can forge.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC signing key. Supplied by environment variable or user-secrets —
    /// never committed. The 32-character floor is the security requirement, not
    /// a style choice: HMAC-SHA256's strength is capped by the key length, and
    /// a short key is brute-forceable offline from a single captured token.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "Jwt:Secret must be at least 32 characters.")]
    public string Secret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Short by design. A leaked access token is only useful for this long, and
    /// the client refreshes transparently, so the user never notices.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// How long a device can stay signed in without re-entering a password.
    /// Rotation on every use means the window for a stolen token is much
    /// shorter than this in practice.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}
