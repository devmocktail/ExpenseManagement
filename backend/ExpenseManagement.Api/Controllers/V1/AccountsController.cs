using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The signed-in account's money pots: bank accounts, cards, wallets, and the
/// cash in their pocket.
///
/// No action takes a user id. <see cref="IAccountService"/> resolves the owner
/// from the token on every call, so there is no parameter a caller could point
/// at somebody else's data, and another account's id answers 404 rather than
/// 403 — a 403 would confirm the id exists.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounts")]
[Produces("application/json")]
public class AccountsController(IAccountService service) : ControllerBase
{
    /// <summary>Lists the account's money pots with their current balances.</summary>
    /// <param name="includeArchived">
    /// Archived accounts are hidden by default: they exist to keep history
    /// intact, not to clutter a picker. Pass true for the management screen.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccountDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountDto>>>> List(
        [FromQuery] bool includeArchived = false)
    {
        var accounts = await service.ListAsync(includeArchived, HttpContext.RequestAborted);
        return Ok(ApiResponse<IReadOnlyList<AccountDto>>.Ok(accounts));
    }

    /// <summary>Fetches one account, with its balance derived at read time.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountDto>>> GetById(Guid id)
    {
        var account = await service.GetByIdAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<AccountDto>.Ok(account));
    }

    /// <summary>Creates an account. The first one a user creates becomes their default.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AccountDto>>> Create(
        [FromBody] CreateAccountRequest request)
    {
        var account = await service.CreateAsync(request, HttpContext.RequestAborted);

        // No explicit `version` route value, matching the rest of the API: an
        // explicitly-supplied value beats the ambient one and would emit a
        // Location of /api/v1.0/... while every other call is /api/v1/....
        return CreatedAtAction(
            nameof(GetById),
            new { id = account.Id },
            ApiResponse<AccountDto>.Ok(account, "Account created successfully"));
    }

    /// <summary>
    /// Renames, restyles, re-types or archives an account.
    ///
    /// There is no currency field: changing it would reinterpret every amount
    /// already recorded against the account without touching a single row.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AccountDto>>> Update(
        Guid id,
        [FromBody] UpdateAccountRequest request)
    {
        var account = await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return Ok(ApiResponse<AccountDto>.Ok(account, "Account updated successfully"));
    }

    /// <summary>
    /// Deletes an account that nothing references.
    ///
    /// An account with transactions or transfers is refused with 422: deleting
    /// it would either orphan that history or take it along, and both rewrite
    /// what the user actually spent. Archive it instead.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await service.DeleteAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Account deleted successfully"));
    }

    /// <summary>Makes this the account pre-selected when adding a transaction.</summary>
    [HttpPost("{id:guid}/default")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AccountDto>>> SetDefault(Guid id)
    {
        var account = await service.SetDefaultAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<AccountDto>.Ok(account, "Default account updated"));
    }
}
