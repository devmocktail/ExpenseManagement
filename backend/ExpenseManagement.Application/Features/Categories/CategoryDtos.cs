using System.Linq.Expressions;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Categories;

/// <summary>
/// A category as the mobile client sees it. Mirrors <c>Category</c> in
/// <c>mobile/src/types/api.ts</c> field for field; System.Text.Json is
/// configured for camelCase, so the names cross the wire unchanged in meaning.
///
/// <c>TransactionCount</c> is the live transaction count for this category:
/// the client shows it in the delete confirmation, so "this will move 12
/// entries" is answered before the request is sent rather than after it is
/// refused.
/// </summary>
public sealed record CategoryDto(
    Guid Id,
    string Name,
    TransactionType Type,
    string Icon,
    string Color,
    bool IsSystem,
    int SortOrder,
    int TransactionCount);

/// <summary>New category. <c>SortOrder</c> is optional — omitted, it appends to the list.</summary>
public sealed record CreateCategoryRequest(
    string Name,
    TransactionType Type,
    string Icon,
    string Color,
    int? SortOrder);

/// <summary>
/// Edit of an existing category. There is deliberately no <c>Type</c> here, for
/// both system and user categories: flipping a bucket from expense to income
/// would move every transaction under it to the other side of the ledger
/// without touching a single transaction row, silently rewriting past totals.
/// Moving entries between ledgers is a per-transaction operation, not a rename.
/// </summary>
public sealed record UpdateCategoryRequest(
    string Name,
    string Icon,
    string Color,
    int? SortOrder);

/// <summary>
/// Delete instruction. The target is required only when the category is
/// actually in use; the server refuses the delete rather than orphan history.
/// </summary>
public sealed record DeleteCategoryRequest(Guid? ReassignToCategoryId);

/// <summary>
/// The single definition of how a <see cref="Category"/> becomes a
/// <see cref="CategoryDto"/>, shared by the list and single-row queries so the
/// two can never drift apart.
/// </summary>
public static class CategoryMappings
{
    /// <summary>
    /// Built per caller rather than exposed as a static expression so the
    /// correlated transaction count carries the same explicit ownership
    /// predicate as the outer query, instead of leaning solely on the
    /// DbContext's ownership filter. The count stays a subquery in SQL — no
    /// rows are pulled back to be counted here.
    /// </summary>
    public static Expression<Func<Category, CategoryDto>> ToDto(Guid userId) =>
        category => new CategoryDto(
            category.Id,
            category.Name,
            category.Type,
            category.Icon,
            category.Color,
            category.IsSystem,
            category.SortOrder,
            category.Transactions.Count(transaction => transaction.UserId == userId));
}
