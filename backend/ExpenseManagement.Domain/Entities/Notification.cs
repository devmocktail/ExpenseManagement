using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// An in-app notification record. Written when the event is detected and
/// delivered separately, so a push failure never loses the message.
/// </summary>
public class Notification : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>JSON payload for deep-linking, e.g. the budget or transaction to open.</summary>
    public string? DataJson { get; set; }

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>When the message should be delivered; past-dated means "as soon as possible".</summary>
    public DateTimeOffset ScheduledFor { get; set; }

    public DateTimeOffset? SentAt { get; set; }
    public int DeliveryAttempts { get; set; }
    public string? LastDeliveryError { get; set; }

    /// <summary>
    /// Stable key for the event this notification represents, e.g.
    /// <c>budget:{id}:2026-09:90</c>. Unique per user so a re-run of the
    /// scheduler cannot notify twice about the same thing.
    /// </summary>
    public string DeduplicationKey { get; set; } = string.Empty;
}
