using ExpenseManagement.Domain.Entities;

namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// Finds a push-token registration without the ownership filter in the way.
///
/// Device registration is the one place a row legitimately changes hands. The
/// OS hands the same push token to whichever account is signed in on that
/// handset, so the row has to be findable while it still belongs to the
/// previous user — otherwise registration inserts a duplicate, the unique index
/// rejects it, and the old owner keeps receiving this user's alerts.
///
/// It gets its own interface for the same reason
/// <see cref="IRecurringScheduleReader"/> does: suspending a global query filter
/// needs that filter's name, and the names are a persistence detail the
/// application layer cannot reference. The escape hatch is one lookup by an
/// exact token — a value the caller must already hold — so it can confirm
/// nothing an attacker did not already know and enumerate nothing at all.
/// </summary>
public interface IDeviceTokenReader
{
    /// <summary>
    /// The registration for <paramref name="token"/>, whoever currently owns it,
    /// or null when the token has never been registered.
    ///
    /// The entity must come back <em>tracked</em> by the same context the
    /// application layer saves through: the caller's whole reason for asking is
    /// to move the row to the signed-in user, and a no-tracking result would
    /// turn that reassignment into a silent no-op.
    /// </summary>
    Task<DeviceToken?> FindByTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Live push tokens belonging to <paramref name="userId"/>.
    ///
    /// Returns bare token strings — no device names, no ids, nothing that
    /// describes a person — and only ever for the single account the caller
    /// names. Used by the delivery worker, which iterates users it has already
    /// resolved from the notification queue rather than scanning for them.
    /// </summary>
    Task<IReadOnlyList<string>> GetActiveTokensAsync(Guid userId, CancellationToken cancellationToken);
}
