using ExpenseManagement.Domain.Entities;

namespace ExpenseManagement.Application.Features.Auth;

// Request records use init-only properties with defaults rather than positional
// parameters: System.Text.Json leaves an absent JSON member at its default, and
// a positional record would happily bind null into a non-nullable string and
// hide it from the nullable analysis. A default plus a validator surfaces the
// same problem as a field-level message the client can render.

/// <summary>Wire shape of <c>RegisterRequest</c> in <c>mobile/src/types/api.ts</c>.</summary>
public sealed record RegisterRequest
{
    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    public bool AcceptedTerms { get; init; }

    /// <summary>Optional client hint. Ignored unless it is a three-letter ISO 4217 code.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>Optional client hint. Ignored unless this server can resolve the zone.</summary>
    public string? TimeZoneId { get; init; }
}

public sealed record LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// Not declared in <c>api.ts</c> as a named type — the client posts a bare
/// <c>{ refreshToken }</c> to <c>/auth/logout</c>, which is this shape.
/// </summary>
public sealed record LogoutRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    public string Email { get; init; } = string.Empty;

    /// <summary>The single-use token from the reset email, not a password.</summary>
    public string Token { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;
}

/// <summary>
/// The pair a client needs to stay signed in. <see cref="RefreshToken"/> is the
/// raw value and is the only time it is ever visible — the server keeps a hash.
/// </summary>
public sealed record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Response of register and login. The wire type is <c>AuthTokens &amp; { user }</c>,
/// an intersection, so the token fields are flattened here rather than nested
/// under a <c>tokens</c> object — a nested shape would not deserialise on the client.
/// </summary>
public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileDto User);

public sealed record UserProfileDto(
    Guid Id,
    string FullName,
    string Email,
    bool EmailConfirmed,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

/// <summary>
/// Coarse facts about the calling connection, filled in by the API layer from
/// the request's transport metadata. Deliberately a separate parameter and not
/// part of any request DTO: an IP or user agent taken from a JSON body would be
/// attacker-controlled, and these end up on session rows a user is shown.
/// </summary>
public sealed record ClientContext(string? IpAddress, string? UserAgent);

/// <summary>
/// Shaping for the auth feature.
///
/// Unlike the other features these are plain functions rather than
/// <c>Expression&lt;Func&lt;,&gt;&gt;</c> projections, and that is forced rather
/// than chosen: <see cref="ApplicationUser"/> is owned by Identity's user store
/// and is not exposed on <c>IAppDbContext</c>, and role membership arrives from
/// <c>UserManager.GetRolesAsync</c>. There is no <c>IQueryable</c> to project
/// over, so an expression would only be translated in memory anyway.
/// </summary>
public static class AuthMappings
{
    /// <summary>
    /// Authorised endpoint path, relative to the API root the client already
    /// knows — never a storage key and never a public URL, because avatars are
    /// served through the bearer-token-protected endpoint like receipts are.
    /// </summary>
    public const string AvatarPath = "/profile/avatar";

    /// <summary>
    /// Takes <see cref="IEnumerable{T}"/> rather than a list because
    /// <c>UserManager.GetRolesAsync</c> hands back an <c>IList&lt;string&gt;</c>,
    /// and <c>IList</c> is not an <c>IReadOnlyList</c> — the copy here is the
    /// conversion, and it also stops the DTO aliasing a mutable collection.
    /// </summary>
    public static UserProfileDto ToProfile(ApplicationUser user, IEnumerable<string> roles) =>
        new(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            user.EmailConfirmed,
            user.AvatarStorageKey is null ? null : AvatarPath,
            user.CreatedAt,
            user.LastLoginAt,
            [.. roles]);

    public static AuthResponseDto ToAuthResponse(AuthTokensDto tokens, UserProfileDto profile) =>
        new(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            tokens.RefreshTokenExpiresAt,
            profile);
}
