using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Budgets;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// Spending caps belonging to the signed-in user, each returned with this
/// window's spend already summed by the server.
/// </summary>
/// <remarks>
/// No action takes a user id. <see cref="IBudgetService"/> resolves the caller
/// from the token, so a request cannot widen what it reads or writes by sending
/// someone else's id, and a budget owned by another user is reported as 404
/// rather than 403 — which would confirm the row exists.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/budgets")]
[Produces("application/json")]
public sealed class BudgetsController(IBudgetService budgetService) : ControllerBase
{
    /// <summary>Lists the caller's active budgets overlapping the period that contains <paramref name="at"/>.</summary>
    /// <param name="at">
    /// Any instant inside the window to evaluate. Omit for now; the server resolves
    /// the containing period in the user's own time zone and month-start day.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BudgetDto>>), StatusCodes.Status200OK)]
    // An unparseable ?at= fails model binding, which the behaviour options in
    // Program.cs render as the same envelope.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BudgetDto>>>> List(
        [FromQuery] DateTimeOffset? at)
    {
        var budgets = await budgetService.ListAsync(at, HttpContext.RequestAborted);

        return Ok(ApiResponse<IReadOnlyList<BudgetDto>>.Ok(budgets));
    }

    /// <summary>Gets one of the caller's budgets.</summary>
    /// <param name="id">The budget's identifier.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BudgetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BudgetDto>>> GetById(Guid id)
    {
        var budget = await budgetService.GetByIdAsync(id, HttpContext.RequestAborted);

        return Ok(ApiResponse<BudgetDto>.Ok(budget));
    }

    /// <summary>Creates a budget for the caller.</summary>
    /// <param name="request">The cap, its period and the category it covers; omit the category for an overall budget.</param>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BudgetDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    // The chosen category may not exist, or may belong to someone else.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    // A budget for the same category already covers this window.
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<BudgetDto>>> Create(
        [FromBody] CreateBudgetRequest request)
    {
        var budget = await budgetService.CreateAsync(request, HttpContext.RequestAborted);

        // No explicit `version` route value: an explicitly-supplied value beats
        // the ambient one during link generation, so passing "1.0" emits a
        // Location of /api/v1.0/... while every other call the client makes is
        // /api/v1/.... Letting the ambient value carry through keeps the two
        // spellings identical.
        return CreatedAtAction(
            nameof(GetById),
            new { id = budget.Id },
            ApiResponse<BudgetDto>.Ok(budget, "Budget created successfully"));
    }

    /// <summary>Replaces one of the caller's budgets.</summary>
    /// <param name="id">The budget's identifier.</param>
    /// <param name="request">The full budget; <c>isActive</c> may be omitted to leave the current state alone.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BudgetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<BudgetDto>>> Update(
        Guid id,
        [FromBody] UpdateBudgetRequest request)
    {
        var budget = await budgetService.UpdateAsync(id, request, HttpContext.RequestAborted);

        return Ok(ApiResponse<BudgetDto>.Ok(budget, "Budget updated successfully"));
    }

    /// <summary>Deletes one of the caller's budgets. Past periods stay reportable.</summary>
    /// <param name="id">The budget's identifier.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await budgetService.DeleteAsync(id, HttpContext.RequestAborted);

        // 200 with the envelope rather than 204: the client unwraps a body on
        // every response, and a 204 has none to unwrap.
        return Ok(ApiResponse.Ok("Budget deleted successfully"));
    }
}
