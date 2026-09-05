using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Categories;

/// <summary>
/// Category management for the authenticated account. Every method resolves the
/// owner from the JWT itself, so no overload takes a user id — there is no way
/// for a caller to ask about somebody else's categories.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// The account's categories ordered by sort order, then name. Unpaged by
    /// design: this list drives the category picker, which needs all of it, and
    /// it is bounded by what one person is willing to hand-maintain.
    /// </summary>
    /// <param name="type">Restricts to one side of the ledger; null returns both.</param>
    Task<IReadOnlyList<CategoryDto>> ListAsync(TransactionType? type, CancellationToken cancellationToken);

    /// <summary>Throws <c>NotFoundException</c> for an unknown id and for another account's id alike.</summary>
    Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes a category. A category still referenced by transactions,
    /// budgets or recurring rules can only go if
    /// <paramref name="reassignToCategoryId"/> names another category of the
    /// same type to inherit them; without one the delete is refused so history
    /// is never orphaned.
    /// </summary>
    Task DeleteAsync(Guid id, Guid? reassignToCategoryId, CancellationToken cancellationToken);
}
