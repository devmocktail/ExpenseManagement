using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Notifications;

/// <summary>
/// One in-app notification, mirroring <c>AppNotification</c> in
/// <c>mobile/src/types/api.ts</c> field for field. System.Text.Json is
/// configured for camelCase, so these PascalCase names land on the wire as the
/// client already expects them.
/// </summary>
/// <param name="DataJson">
/// The stored payload, still as text. Kept off the wire and surfaced through
/// <see cref="Data"/> instead, so the DTO carries the column the query can
/// actually select and the client gets the shape it actually declared.
/// </param>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    [property: JsonIgnore] string? DataJson,
    bool IsRead,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// The deep-link payload as JSON, not as a string containing JSON: the
    /// client's type is <c>Record&lt;string, unknown&gt; | null</c>, and handing
    /// it an escaped string would make every consumer parse the same value a
    /// second time.
    ///
    /// Parsed here rather than inside the projection because no database can
    /// deserialise into a CLR object mid-query — the column comes back as text
    /// and is turned into JSON once, as the response is written.
    /// </summary>
    public JsonElement? Data => Parse(DataJson);

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var document = JsonDocument.Parse(json);

            // Clone detaches the value from the pooled buffers the document owns
            // and returns on dispose; the uncloned RootElement would be reading
            // memory that has already been handed back.
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // A payload an older build wrote badly costs that one notification
            // its deep link. Failing here would cost the user the whole list.
            return null;
        }
    }
}

/// <summary>
/// Options for the notification list.
///
/// The response is a plain array, as the wire contract specifies, which is
/// exactly why the cap lives here: notification history grows without bound and
/// the client renders it as one screen. <see cref="Limit"/> clamps rather than
/// rejects, for the same reason <c>PaginationRequest</c> does — a client asking
/// for ten thousand gets the maximum, not a 400 it cannot recover from.
/// </summary>
public sealed class NotificationQueryRequest
{
    public const int MaxLimit = 100;
    public const int DefaultLimit = 50;

    private int _limit = DefaultLimit;

    /// <summary>Restricts the list to what still needs the user's attention.</summary>
    public bool UnreadOnly { get; set; }

    public int Limit
    {
        get => _limit;
        set => _limit = value switch
        {
            < 1 => DefaultLimit,
            > MaxLimit => MaxLimit,
            _ => value,
        };
    }
}

/// <summary>
/// A device announcing itself for push delivery. Sent on every cold start, not
/// just the first, so this is an upsert and never an error.
///
/// No member is <c>required</c> on purpose: System.Text.Json rejects a payload
/// missing a required member with an exception the validation pipeline cannot
/// turn into a per-field message, so absence is caught by the validator, which
/// can.
/// </summary>
public sealed record RegisterDeviceRequest(
    string Token,
    DevicePlatform Platform,
    string? DeviceName = null,
    string? AppVersion = null);

/// <summary>
/// The body of a device deregistration. The service takes the token itself —
/// this exists so the request has a shape the model binder and the validation
/// pipeline can work with before it gets there.
/// </summary>
public sealed record UnregisterDeviceRequest(string Token);

/// <summary>
/// The single definition of how a <see cref="Notification"/> becomes a
/// <see cref="NotificationDto"/>. Held in one place so the list query and any
/// later reader (the delivery worker's audit view, a detail endpoint) project
/// identical columns instead of one of them quietly materialising entities.
/// </summary>
internal static class NotificationMappings
{
    /// <summary>
    /// A get-only property so the tree is built once per process rather than per
    /// request. Note what is absent: <c>Body</c>'s siblings <c>SentAt</c>,
    /// <c>DeliveryAttempts</c> and <c>LastDeliveryError</c> are delivery
    /// mechanics, and putting them on the wire would tell a client about retry
    /// state it can do nothing with.
    /// </summary>
    public static Expression<Func<Notification, NotificationDto>> ToDto { get; } = notification =>
        new NotificationDto(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Body,
            notification.DataJson,
            notification.IsRead,
            notification.CreatedAt);
}
