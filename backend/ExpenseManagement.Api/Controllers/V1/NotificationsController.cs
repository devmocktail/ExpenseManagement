using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The signed-in account's notification inbox, and the devices those
/// notifications are pushed to.
/// </summary>
/// <remarks>
/// No action takes a user id. <see cref="INotificationService"/> resolves the
/// caller from the token on every call, so there is no parameter a request could
/// point at somebody else's inbox or somebody else's handset.
///
/// <c>INotificationService.EnqueueAsync</c> is deliberately not exposed. It is
/// the one member that accepts a user id, because alerting runs on behalf of a
/// user who is not the caller and so has no principal to read — which is exactly
/// what makes it unsafe to reach from HTTP: nothing inside it checks that the
/// caller may write to that account, since by construction the caller is the
/// server. Routing it would let anyone forge a notification to any user.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Produces("application/json")]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    /// <summary>Lists the caller's notifications, newest first.</summary>
    /// <param name="request">
    /// Optional filter and cap. The app sends neither on a cold start, so both
    /// carry defaults; <c>limit</c> clamps to the maximum rather than rejecting,
    /// which is why an over-large value is not a 400.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NotificationDto>>), StatusCodes.Status200OK)]
    // A non-numeric ?limit= or non-boolean ?unreadOnly= fails model binding,
    // which the behaviour options in Program.cs render as the same envelope.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationDto>>>> List(
        [FromQuery] NotificationQueryRequest request)
    {
        var notifications = await service.ListAsync(request, HttpContext.RequestAborted);
        return Ok(ApiResponse<IReadOnlyList<NotificationDto>>.Ok(notifications));
    }

    /// <summary>Marks one notification read.</summary>
    /// <param name="id">
    /// The notification to mark. Another account's id answers 404, not 403 —
    /// a 403 would confirm the id is real. Re-marking one already read succeeds
    /// and keeps the original read time, because the client fires this on every
    /// open of a message it may already have opened.
    /// </param>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> MarkRead(Guid id)
    {
        await service.MarkReadAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Notification marked as read"));
    }

    /// <summary>Clears the unread badge by marking every unread notification read.</summary>
    [HttpPut("read-all")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> MarkAllRead()
    {
        await service.MarkAllReadAsync(HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Notifications marked as read"));
    }

    /// <summary>Registers, or re-registers, this installation for push delivery.</summary>
    /// <param name="request">
    /// The Expo push token, the platform it belongs to, and optional labels for
    /// a "your devices" list.
    /// </param>
    /// <remarks>
    /// 200 rather than 201: the app sends this on every cold start, so the
    /// service upserts and a repeat registration creates nothing. There is also
    /// no GetById on this controller to point a <c>Location</c> header at, and a
    /// per-device read endpoint would put a live push address on the wire.
    /// </remarks>
    [HttpPost("devices")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    // Two cold starts racing can collide on the token's unique index in a way the
    // service cannot fully pre-check.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    // A token that survives binding but normalises to nothing.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse>> RegisterDevice(
        [FromBody] RegisterDeviceRequest request)
    {
        await service.RegisterDeviceAsync(request, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Device registered successfully"));
    }

    /// <summary>Stops push delivery to one of the caller's devices.</summary>
    /// <param name="request">
    /// Carries the token in the body rather than the URL: a push token is a
    /// delivery credential, and a query string is logged by every proxy and
    /// access log between the handset and here.
    /// </param>
    /// <remarks>
    /// Idempotent, and 200 even when nothing matched. Sign-out calls this
    /// best-effort while it is already tearing the session down and cannot act on
    /// a failure, so an unknown token is a no-op rather than a 404 — which would
    /// also disclose whether a token exists.
    /// </remarks>
    [HttpDelete("devices")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> UnregisterDevice(
        [FromBody] UnregisterDeviceRequest request)
    {
        await service.UnregisterDeviceAsync(request.Token, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Device unregistered successfully"));
    }
}
