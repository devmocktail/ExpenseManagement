using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Application.Features.Transfers;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// Money moved between two of the signed-in account's own pots — bank to
/// wallet, bank to cash in hand, a card bill paid from a current account.
///
/// Nothing here is income or spending. A transfer conserves money, so these
/// rows change the two balances they name and appear in no total the user sees
/// as what they spent. That separation is structural: the dashboard, the
/// analytics summary and the budget evaluator all read Transactions, which
/// transfers are not.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/transfers")]
[Produces("application/json")]
public class TransfersController(ITransferService service) : ControllerBase
{
    /// <summary>Lists transfers, newest first.</summary>
    /// <param name="query">
    /// Optional filters. <c>accountId</c> matches either end — "what moved in
    /// and out of my wallet" is one question, not two.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TransferDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<TransferDto>>>> List(
        [FromQuery] TransferQueryRequest query)
    {
        var transfers = await service.ListAsync(query, HttpContext.RequestAborted);
        return Ok(ApiResponse<PagedResult<TransferDto>>.Ok(transfers));
    }

    /// <summary>Fetches one transfer by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransferDto>>> GetById(Guid id)
    {
        var transfer = await service.GetByIdAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<TransferDto>.Ok(transfer));
    }

    /// <summary>
    /// Records a movement between two accounts.
    ///
    /// Both must belong to the caller, differ from each other, and share a
    /// currency — converting would need a rate, and inventing one would make
    /// the amount leaving disagree with the amount arriving.
    /// </summary>
    /// <param name="request">
    /// Source, destination, amount, date, and an optional <c>clientReference</c>
    /// for offline replay: repeating the same key returns the transfer it
    /// already created rather than moving the money twice.
    /// </param>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TransferDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<TransferDto>>> Create(
        [FromBody] CreateTransferRequest request)
    {
        var transfer = await service.CreateAsync(request, HttpContext.RequestAborted);

        return CreatedAtAction(
            nameof(GetById),
            new { id = transfer.Id },
            ApiResponse<TransferDto>.Ok(transfer, "Transfer recorded successfully"));
    }

    /// <summary>Edits a transfer, including moving either end to a different account.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<TransferDto>>> Update(
        Guid id,
        [FromBody] UpdateTransferRequest request)
    {
        var transfer = await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return Ok(ApiResponse<TransferDto>.Ok(transfer, "Transfer updated successfully"));
    }

    /// <summary>
    /// Removes a transfer. Both balances move back by the amount it carried,
    /// because they are derived from these rows rather than stored.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await service.DeleteAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Transfer deleted successfully"));
    }
}
