using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Application.Features.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The caller's own transactions.
///
/// No action takes a user id. <see cref="ITransactionService"/> resolves the
/// owner from the authenticated principal, and a row belonging to someone else
/// is reported as 404 rather than 403 so the API cannot be used to probe which
/// ids exist.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/transactions")]
[Produces("application/json")]
public class TransactionsController(ITransactionService service) : ControllerBase
{
    /// <summary>Lists the caller's transactions, filtered, sorted and paged.</summary>
    /// <param name="request">
    /// Filters, sort and pagination, bound from the query string as a whole.
    /// Page and page size clamp rather than reject, so an out-of-range value
    /// returns a capped page instead of a 400.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<TransactionDto>>>> List(
        [FromQuery] TransactionQueryRequest request)
    {
        var result = await service.ListAsync(request, HttpContext.RequestAborted);

        return Ok(ApiResponse<PagedResult<TransactionDto>>.Ok(result));
    }

    /// <summary>Gets one of the caller's transactions by id.</summary>
    /// <param name="id">The transaction id.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransactionDto>>> GetById(Guid id)
    {
        var result = await service.GetByIdAsync(id, HttpContext.RequestAborted);

        return Ok(ApiResponse<TransactionDto>.Ok(result));
    }

    /// <summary>
    /// Creates a transaction. A request that repeats a <c>clientReference</c>
    /// this user has already sent updates that row instead of adding a second
    /// one, so an offline queue can be replayed safely; such a replay still
    /// answers 201 pointing at the existing row.
    /// </summary>
    /// <param name="request">The transaction to create.</param>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<TransactionDto>>> Create(
        [FromBody] CreateTransactionRequest request)
    {
        var result = await service.CreateAsync(request, HttpContext.RequestAborted);

        // No explicit `version` route value: an explicitly-supplied value beats
        // the ambient one during link generation, so passing "1.0" emits a
        // Location of /api/v1.0/... while every other call the client makes is
        // /api/v1/.... Letting the ambient value carry through keeps the two
        // spellings identical.
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<TransactionDto>.Ok(result, "Transaction created successfully"));
    }

    /// <summary>Replaces one of the caller's transactions.</summary>
    /// <param name="id">The transaction id.</param>
    /// <param name="request">
    /// The new values. Carries no client reference: that key identifies the row,
    /// so an update must not be able to rewrite it.
    /// </param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<TransactionDto>>> Update(
        Guid id,
        [FromBody] UpdateTransactionRequest request)
    {
        var result = await service.UpdateAsync(id, request, HttpContext.RequestAborted);

        return Ok(ApiResponse<TransactionDto>.Ok(result, "Transaction updated successfully"));
    }

    /// <summary>Soft deletes one of the caller's transactions and its receipts.</summary>
    /// <param name="id">The transaction id.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await service.DeleteAsync(id, HttpContext.RequestAborted);

        // 200 with the envelope, not 204: the client unwraps a body on every
        // response and a 204 has none.
        return Ok(ApiResponse.Ok("Transaction deleted successfully"));
    }
}
