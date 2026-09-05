using System.Security.Claims;
using ExpenseManagement.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ExpenseManagement.Infrastructure.Identity;

/// <summary>
/// Resolves the caller's identity from the validated JWT.
///
/// This is the ONLY place a user id enters the application layer. Nothing reads
/// a user id from a route parameter, a query string or a request body, because
/// any of those would let a caller name someone else and read their data
/// (Rule 7). By the time a claim is visible here the bearer middleware has
/// already verified the token's signature, issuer, audience and lifetime.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var principal = Principal;
            if (principal?.Identity?.IsAuthenticated != true) return null;

            // ASP.NET Core remaps "sub" to ClaimTypes.NameIdentifier by default,
            // but that mapping can be turned off — so both spellings are checked
            // rather than depending on middleware configuration that lives in a
            // different file.
            var raw =
                principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public bool IsAuthenticated => UserId is not null;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException(
            "This operation requires an authenticated user, but no user id was present on the request.");
}
