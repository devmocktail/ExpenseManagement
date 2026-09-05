namespace ExpenseManagement.Application.Features.Auth;

/// <summary>
/// Account lifecycle and session issuance.
///
/// Every method here either issues no tokens or issues a complete, consistent
/// set; there is no partial success. <paramref name="client"/> parameters are
/// optional so a caller that has no transport metadata (a background job, a
/// test) still compiles — the token rows simply record nothing about the origin.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Creates the account, its settings row and its default categories in one
    /// transaction, then signs the new user in.
    /// </summary>
    /// <exception cref="Domain.Exceptions.ConflictException">The email is already registered.</exception>
    Task<AuthResponseDto> RegisterAsync(
        RegisterRequest request,
        ClientContext? client = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies credentials and starts a new refresh-token family.
    /// Wrong password, unknown address and closed account are indistinguishable
    /// to the caller, by message and by response time.
    /// </summary>
    Task<AuthResponseDto> LoginAsync(
        LoginRequest request,
        ClientContext? client = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates a refresh token, returning its successor in the same family.
    /// Presenting a token that was already spent revokes the entire family.
    /// </summary>
    Task<AuthTokensDto> RefreshAsync(
        RefreshRequest request,
        ClientContext? client = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the presented token's whole family. Succeeds even when the token
    /// is unknown — a logout must never fail, and must never confirm whether a
    /// value was real.
    /// </summary>
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails a reset token when the address belongs to an active account.
    /// Always succeeds, whether it does or not.
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Consumes a reset token, sets the new password and signs out every device.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes the signed-in user's password and signs out every device.</summary>
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
