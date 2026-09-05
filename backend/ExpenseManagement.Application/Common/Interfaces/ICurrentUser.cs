namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// The authenticated principal, resolved from JWT claims — never from anything
/// the client puts in a route, query string or body. This is the single source
/// of identity for the whole application layer.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Null only for unauthenticated requests (login, register, refresh).</summary>
    Guid? UserId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);

    /// <summary>
    /// The user id, or a <see cref="UnauthorizedAccessException"/> if there is none.
    /// Services that require a user call this so a misconfigured
    /// <c>[AllowAnonymous]</c> fails loudly instead of leaking another user's data.
    /// </summary>
    Guid RequireUserId();
}
