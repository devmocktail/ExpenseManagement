using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The home screen's read, composed server-side.
/// </summary>
/// <remarks>
/// One endpoint on purpose: balance, recent transactions, category breakdown,
/// weekly trend, budget progress and currency were six calls, so the home screen
/// painted in six stages over a mobile connection. Composing them here makes a
/// cold start a single round trip and — because every figure comes from one
/// snapshot — internally consistent.
///
/// No action takes a user id. <see cref="IDashboardService"/> resolves the caller
/// from the token, so a request cannot read someone else's dashboard by sending
/// their id.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Gets the caller's dashboard for the period containing <paramref name="at"/>.</summary>
    /// <param name="at">
    /// Any instant inside the period to show. Omit for now, which is what the app
    /// sends on a cold start; an explicit instant is how the user pages back
    /// through previous months. The server resolves the containing period in the
    /// caller's own time zone and month-start day.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DashboardResponseDto>), StatusCodes.Status200OK)]
    // An unparseable ?at= fails model binding, which the behaviour options in
    // Program.cs render as the same envelope.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    // The caller's settings row is missing, so there is no time zone or currency
    // to resolve the period in.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    // An anchor far enough out to overflow the period arithmetic is rejected as a
    // request error rather than surfacing as a 500.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<DashboardResponseDto>>> Get(
        [FromQuery] DateTimeOffset? at)
    {
        var dashboard = await dashboardService.GetAsync(at, HttpContext.RequestAborted);

        return Ok(ApiResponse<DashboardResponseDto>.Ok(dashboard));
    }
}
