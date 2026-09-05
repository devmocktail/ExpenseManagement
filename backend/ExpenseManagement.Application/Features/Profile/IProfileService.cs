using ExpenseManagement.Application.Features.Auth;

namespace ExpenseManagement.Application.Features.Profile;

/// <summary>
/// The signed-in account: who it is, how it is configured, how it leaves.
///
/// Nothing here takes a user id. Every method resolves the caller from the
/// authenticated principal, so there is no parameter an attacker could point at
/// somebody else's account.
/// </summary>
public interface IProfileService
{
    /// <summary>The caller's profile, including role membership.</summary>
    /// <exception cref="Domain.Exceptions.NotFoundException">The account no longer exists.</exception>
    Task<UserProfileDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the display name. The email address is deliberately not settable
    /// here — changing it has to prove control of the new mailbox before it can
    /// become a sign-in identity, and that flow does not exist yet.
    /// </summary>
    Task<UserProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's preferences. An account with no settings row answers with
    /// the defaults rather than a 404: the row is created with the account, and
    /// a settings screen is the wrong place to discover that it was not.
    /// </summary>
    Task<UserSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the settings present on the request and leaves the rest alone.
    /// Returns the full merged settings so the client never has to guess what a
    /// partial write left behind.
    /// </summary>
    /// <exception cref="Domain.Exceptions.BusinessRuleException">
    /// The merged thresholds would put the critical alert below the warning.
    /// </exception>
    Task<UserSettingsDto> UpdateSettingsAsync(
        UpdateSettingsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the account after re-verifying the password: soft-deletes the
    /// user, revokes every refresh token and deactivates every push device.
    /// </summary>
    /// <exception cref="Domain.Exceptions.BusinessRuleException">The password is wrong.</exception>
    Task DeleteAccountAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's transactions in <c>[from, to)</c> as CSV or JSON text, at
    /// most <see cref="TransactionExport.MaxRows"/> rows — a truncated export
    /// says so in its own body rather than looking complete.
    /// </summary>
    Task<string> ExportTransactionsAsync(
        ExportFormat format,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}
