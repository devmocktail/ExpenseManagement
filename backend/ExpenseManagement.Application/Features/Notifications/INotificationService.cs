using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Notifications;

/// <summary>
/// The caller's notification inbox and the devices it is delivered to.
///
/// Every request-facing member resolves the user from the authenticated
/// principal, never from an argument. A notification that does not exist and one
/// belonging to another account both raise <c>NotFoundException</c>: answering
/// 403 for the second case would confirm the id is real.
///
/// <see cref="EnqueueAsync"/> is the one exception, and deliberately so — see
/// its own remarks.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// The caller's notifications, newest first, capped by
    /// <see cref="NotificationQueryRequest.Limit"/>. Notifications scheduled for
    /// a future instant are withheld until that instant passes.
    /// </summary>
    Task<IReadOnlyList<NotificationDto>> ListAsync(
        NotificationQueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one notification read. Re-marking an already-read notification
    /// succeeds and leaves the original read time alone, because the client
    /// fires this on every open of a message it may already have opened.
    /// </summary>
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Clears the unread badge in a single statement, without loading a row.</summary>
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers, or re-registers, this installation for push.
    ///
    /// A token already held by a different account is <em>moved</em> to the
    /// caller rather than duplicated: the OS reassigns a push token when someone
    /// else signs in on the same handset, and leaving the old row in place would
    /// deliver this user's budget alerts to the previous one's lock screen.
    /// </summary>
    Task RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops push delivery to one of the caller's devices. Idempotent: sign-out
    /// calls this best-effort and cannot act on a failure.
    /// </summary>
    Task UnregisterDeviceAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a notification for <paramref name="userId"/>, unless that user has
    /// switched this kind off or the same event has already been queued.
    /// </summary>
    /// <remarks>
    /// This is the one member that takes a user id, and it is not a hole in the
    /// rule that ids never come from a request: alerting runs on behalf of a user
    /// who is not the caller — a background sweep detecting an overspent budget,
    /// or a transaction write tripping a threshold — so there is no principal to
    /// read. It must never be reachable from a controller; nothing here checks
    /// that the caller may write to this account, because by construction the
    /// caller is the server.
    /// </remarks>
    /// <param name="deduplicationKey">
    /// Stable identity for the event, e.g. <c>budget:{id}:2026-09:75</c>. The
    /// unique index on (user, key) is what stops an at-least-once scheduler
    /// telling someone twice that they have used 75% of their food budget.
    /// </param>
    /// <param name="data">
    /// Deep-link payload, serialised to JSON for the client. Kept small: it is
    /// stored in a bounded column and dropped rather than truncated if it
    /// overflows.
    /// </param>
    /// <param name="scheduledFor">
    /// When the message becomes deliverable and visible in the list. A past
    /// instant means "as soon as possible".
    /// </param>
    Task EnqueueAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string deduplicationKey,
        object? data,
        DateTimeOffset scheduledFor,
        CancellationToken cancellationToken = default);
}
