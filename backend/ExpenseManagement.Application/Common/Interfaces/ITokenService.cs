using System.Security.Claims;
using ExpenseManagement.Domain.Entities;

namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>A freshly minted access token and its expiry.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// A refresh token. <see cref="RawValue"/> is returned to the client exactly
/// once and never persisted — only <see cref="Entity"/>'s hash is stored.
/// </summary>
public sealed record IssuedRefreshToken(string RawValue, RefreshToken Entity);

public interface ITokenService
{
    AccessToken CreateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>
    /// Mints a refresh token. Pass an existing <paramref name="familyId"/> when
    /// rotating so the successor stays in the same device family; pass null on
    /// a fresh login to start a new family.
    /// </summary>
    IssuedRefreshToken CreateRefreshToken(Guid userId, Guid? familyId, string? ipAddress, string? userAgent);

    /// <summary>Hashes a raw token the same way it was hashed at issue time, for lookup.</summary>
    string HashToken(string rawToken);

    /// <summary>
    /// Validates an expired access token's signature to recover its claims
    /// during refresh. Lifetime validation is deliberately skipped; every other
    /// check (signature, issuer, audience, algorithm) still applies.
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken);
}
