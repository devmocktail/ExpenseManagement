using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Categories;
using ExpenseManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// The signed-in account's expense and income categories.
///
/// No action takes a user id. <see cref="ICategoryService"/> resolves the owner
/// from the token on every call, so there is no parameter a caller could point
/// at somebody else's data.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories")]
[Produces("application/json")]
public class CategoriesController(ICategoryService service) : ControllerBase
{
    /// <summary>Lists the account's categories, ordered by sort order then name.</summary>
    /// <param name="type">
    /// Optional filter: <c>Expense</c> or <c>Income</c>. Omitted, both sides of
    /// the ledger come back — which is what the category picker asks for.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryDto>>>> List(
        [FromQuery] TransactionType? type)
    {
        var categories = await service.ListAsync(type, HttpContext.RequestAborted);
        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(categories));
    }

    /// <summary>Fetches one category by id.</summary>
    /// <param name="id">The category's id. Another account's id answers 404, not 403.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetById(Guid id)
    {
        var category = await service.GetByIdAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<CategoryDto>.Ok(category));
    }

    /// <summary>Creates a category for the account.</summary>
    /// <param name="request">Name, type, icon, colour, and an optional sort order.</param>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create(
        [FromBody] CreateCategoryRequest request)
    {
        var category = await service.CreateAsync(request, HttpContext.RequestAborted);

        // No explicit `version` route value: an explicitly-supplied value beats
        // the ambient one during link generation, so passing "1.0" emits a
        // Location of /api/v1.0/... while every other call the client makes is
        // /api/v1/.... Letting the ambient value carry through keeps the two
        // spellings identical.
        return CreatedAtAction(
            nameof(GetById),
            new { id = category.Id },
            ApiResponse<CategoryDto>.Ok(category, "Category created successfully"));
    }

    /// <summary>Renames or restyles a category, system categories included.</summary>
    /// <param name="id">The category to edit.</param>
    /// <param name="request">
    /// Name, icon, colour, and an optional sort order. There is no type field:
    /// a category never crosses the ledger, so past totals cannot be rewritten
    /// by an edit.
    /// </param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request)
    {
        var category = await service.UpdateAsync(id, request, HttpContext.RequestAborted);
        return Ok(ApiResponse<CategoryDto>.Ok(category, "Category updated successfully"));
    }

    /// <summary>Deletes a category, optionally moving everything under it somewhere else.</summary>
    /// <param name="id">The category to delete. Built-in categories are refused with 422.</param>
    /// <param name="request">
    /// Optional. Carries the category that should inherit existing transactions,
    /// budgets and schedules. Required only when the category is actually in
    /// use — deleting an unused one legitimately sends no body at all, hence
    /// <see cref="EmptyBodyBehavior.Allow"/> rather than an implicit 400.
    /// </param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse>> Delete(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DeleteCategoryRequest? request)
    {
        await service.DeleteAsync(id, request?.ReassignToCategoryId, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Category deleted successfully"));
    }
}
