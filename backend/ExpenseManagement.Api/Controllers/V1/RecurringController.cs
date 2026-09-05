using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Recurring;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The signed-in account's recurring schedules.
///
/// No action takes a user id. <see cref="IRecurringService"/> resolves the owner
/// from the token on every call, so there is no parameter a caller could point
/// at somebody else's schedule; an id that belongs to another account answers
/// 404 rather than 403, which keeps the API from confirming that the id exists.
///
/// Generation is deliberately absent. <c>ProcessDueAsync</c> runs without an
/// authenticated user and bills every account at once, so it belongs to the
/// background worker and must never be reachable over HTTP.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/recurring")]
[Produces("application/json")]
public class RecurringController(IRecurringService service) : ControllerBase
{
    /// <summary>Lists the account's schedules: active first, then paused, each in due order.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RecurringDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecurringDto>>>> List()
    {
        var schedules = await service.ListAsync(HttpContext.RequestAborted);
        return Ok(ApiResponse<IReadOnlyList<RecurringDto>>.Ok(schedules));
    }

    /// <summary>Fetches one schedule by id.</summary>
    /// <param name="id">The schedule's id. Another account's id answers 404, not 403.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RecurringDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecurringDto>>> GetById(Guid id)
    {
        var schedule = await service.GetByIdAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<RecurringDto>.Ok(schedule));
    }

    /// <summary>Creates a schedule and computes its first run.</summary>
    /// <param name="request">
    /// Name, direction, amount, category, payment method and the recurrence
    /// itself. There is no currency and no run bookkeeping: a schedule is always
    /// in the account's currency, and the next run date is the scheduler's to
    /// set. A 404 here means the category does not exist for this account; a 422
    /// means the schedule is valid in shape but could never produce anything.
    /// </param>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RecurringDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<RecurringDto>>> Create(
        [FromBody] CreateRecurringRequest request)
    {
        var schedule = await service.CreateAsync(request, HttpContext.RequestAborted);

        // No explicit `version` route value: an explicitly-supplied value beats
        // the ambient one during link generation, so passing "1.0" emits a
        // Location of /api/v1.0/... while every other call the client makes is
        // /api/v1/.... Letting the ambient value carry through keeps the two
        // spellings identical.
        return CreatedAtAction(
            nameof(GetById),
            new { id = schedule.Id },
            ApiResponse<RecurringDto>.Ok(schedule, "Schedule created successfully"));
    }

    /// <summary>Replaces a schedule whole.</summary>
    /// <param name="id">The schedule to edit.</param>
    /// <param name="request">
    /// The same body as create — a schedule is edited whole. The next run date
    /// is recomputed only when the recurrence moves, so editing an amount does
    /// not shift the date the user can see.
    /// </param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RecurringDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<RecurringDto>>> Update(
        Guid id,
        [FromBody] UpdateRecurringRequest request)
    {
        var schedule = await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return Ok(ApiResponse<RecurringDto>.Ok(schedule, "Schedule updated successfully"));
    }

    /// <summary>Pauses or resumes a schedule.</summary>
    /// <param name="id">The schedule to pause or resume.</param>
    /// <param name="request">
    /// The desired state. This is its own endpoint rather than a field on the
    /// update body because it is the change a user makes in a hurry, and it must
    /// not require sending — and so risk overwriting — the whole schedule.
    /// </param>
    [HttpPatch("{id:guid}/paused")]
    [ProducesResponseType(typeof(ApiResponse<RecurringDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecurringDto>>> SetPaused(
        Guid id,
        [FromBody] SetRecurringPausedRequest request)
    {
        var schedule = await service.SetPausedAsync(id, request.IsPaused, HttpContext.RequestAborted);

        return Ok(ApiResponse<RecurringDto>.Ok(
            schedule,
            request.IsPaused ? "Schedule paused successfully" : "Schedule resumed successfully"));
    }

    /// <summary>Deletes a schedule.</summary>
    /// <param name="id">
    /// The schedule to delete. Only the template goes: the transactions it has
    /// already generated are real spending and keep their history.
    /// </param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await service.DeleteAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Schedule deleted successfully"));
    }
}
