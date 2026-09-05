using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Categories;

/// <inheritdoc cref="ICategoryService"/>
public sealed class CategoryService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IAuditService audit) : ICategoryService
{
    /// <summary>
    /// The filtered unique index behind the duplicate-name rule. Matching its
    /// name in a provider's exception message is what lets the application
    /// layer turn a lost race into a 409 without referencing a database driver.
    /// </summary>
    private const string UniqueNameIndex = "UX_Categories_UserId_Name_Type";

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(
        TransactionType? type,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var query = db.Categories
            .AsNoTracking()
            .Where(category => category.UserId == userId);

        // Composed conditionally rather than as `type == null || c.Type == type`:
        // the latter compiles to an OR on a parameter, which stops SQL Server
        // seeking IX_Categories_UserId_Type_SortOrder and costs the picker a scan.
        if (type is not null)
        {
            query = query.Where(category => category.Type == type.Value);
        }

        return await query
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(CategoryMappings.ToDto(userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        return await ProjectAsync(id, userId, cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var name = request.Name.Trim();
        var icon = request.Icon.Trim().ToLowerInvariant();

        // The colour column is a fixed-width nchar(7) guarded by a check
        // constraint, so normalising the case here keeps two swatches that mean
        // the same colour from ever comparing unequal downstream.
        var color = request.Color.Trim().ToUpperInvariant();

        var duplicate = await db.Categories
            .AsNoTracking()
            .AnyAsync(
                category => category.UserId == userId
                    && category.Type == request.Type
                    && category.Name == name,
                cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(DuplicateNameMessage(name, request.Type));
        }

        var category = new Category
        {
            UserId = userId,
            Name = name,
            Type = request.Type,
            Icon = icon,
            Color = color,
            SortOrder = request.SortOrder ?? await NextSortOrderAsync(userId, request.Type, cancellationToken),
            IsSystem = false,
        };

        db.Categories.Add(category);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateName(ex))
        {
            // The check above only narrows the common case. Two taps on a flaky
            // connection can both pass it, and the index is what actually
            // decides; a lost race is the user's own duplicate, not a 500.
            throw new ConflictException(DuplicateNameMessage(name, request.Type));
        }

        // A category that did not exist a moment ago cannot be referenced yet,
        // so its count is zero by construction and a second round-trip to
        // rediscover that would be pure ceremony.
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Type,
            category.Icon,
            category.Color,
            category.IsSystem,
            category.SortOrder,
            TransactionCount: 0);
    }

    public async Task<CategoryDto> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Tracked on purpose: this is a write. The explicit ownership predicate
        // sits on top of the DbContext's global filter so the intent is legible
        // at the call site, and a missing row and a stranger's row give the same
        // 404 - a 403 here would confirm that the id exists.
        var category = await db.Categories
            .Where(c => c.Id == id && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);

        var name = request.Name.Trim();

        // System categories are renamed and restyled freely - only deletion is
        // barred. Type is read off the stored row and never off the request,
        // which is why UpdateCategoryRequest has no Type field at all: there is
        // no path through this method that can move a category across the
        // ledger, so existing transactions cannot change sign behind the user.
        if (!string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await db.Categories
                .AsNoTracking()
                .AnyAsync(
                    c => c.UserId == userId
                        && c.Type == category.Type
                        && c.Name == name
                        && c.Id != id,
                    cancellationToken);

            if (duplicate)
            {
                throw new ConflictException(DuplicateNameMessage(name, category.Type));
            }
        }

        category.Name = name;
        category.Icon = request.Icon.Trim().ToLowerInvariant();
        category.Color = request.Color.Trim().ToUpperInvariant();
        category.SortOrder = request.SortOrder ?? category.SortOrder;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateName(ex))
        {
            throw new ConflictException(DuplicateNameMessage(name, category.Type));
        }

        return await ProjectAsync(id, userId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        Guid? reassignToCategoryId,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var category = await db.Categories
            .Where(c => c.Id == id && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);

        // Every account keeps its seeded set forever: it is the guaranteed
        // landing zone for reassignment, so the delete flow can always offer a
        // target of the right type no matter what the user has removed.
        if (category.IsSystem)
        {
            throw new BusinessRuleException(
                $"\"{category.Name}\" is a built-in category and cannot be deleted. You can rename or restyle it instead.",
                "category_is_system");
        }

        // One statement, three correlated counts - the alternative is three
        // round-trips to answer a single question.
        var usage = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id && c.UserId == userId)
            .Select(c => new
            {
                Transactions = c.Transactions.Count(t => t.UserId == userId),
                Budgets = c.Budgets.Count(b => b.UserId == userId),
                Recurring = c.RecurringTransactions.Count(r => r.UserId == userId),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);

        if (usage.Transactions == 0 && usage.Budgets == 0 && usage.Recurring == 0)
        {
            // Remove on a soft-deletable entity is rewritten into an update by
            // the DbContext, so the row survives for anything that still joins
            // to it while the index's IsDeleted filter frees the name for reuse.
            db.Categories.Remove(category);
            await db.SaveChangesAsync(cancellationToken);

            await audit.LogAsync(
                action: "category.deleted",
                userId: userId,
                entityName: nameof(Category),
                entityId: id,
                metadata: new { category.Name, reassignedTo = (Guid?)null },
                cancellationToken: cancellationToken);

            return;
        }

        if (reassignToCategoryId is null)
        {
            throw new ConflictException(
                InUseMessage(category.Name, usage.Transactions, usage.Budgets, usage.Recurring));
        }

        var targetId = reassignToCategoryId.Value;

        if (targetId == id)
        {
            throw new BusinessRuleException(
                "A category cannot be moved into itself. Choose a different category for these entries.",
                "category_reassign_self");
        }

        var target = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == targetId && c.UserId == userId)
            .Select(c => new { c.Name, c.Type })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Category), targetId);

        // Transactions and schedules carry their own Type alongside their
        // category's. Moving an expense under an income bucket would leave the
        // two disagreeing, and every report that groups by category would then
        // count the same rupee on the wrong side of the ledger.
        if (target.Type != category.Type)
        {
            throw new BusinessRuleException(
                $"\"{target.Name}\" is an {Describe(target.Type)} category, so {Describe(category.Type)} entries cannot be moved into it.",
                "category_type_mismatch");
        }

        await GuardBudgetCollisionAsync(userId, id, targetId, category.Name, target.Name, cancellationToken);

        var now = clock.UtcNow;

        // Disposing an uncommitted transaction rolls it back, so a failure
        // anywhere below leaves the category and its dependents untouched
        // rather than half-moved.
        await using var databaseTransaction = await db.BeginTransactionAsync(cancellationToken);

        // Set-based updates: a category with tens of thousands of transactions
        // is ordinary after a few years, and loading them to rewrite one FK each
        // would cost a tracked entity per row. ExecuteUpdate goes around
        // SaveChanges, which also means the audit interceptor never sees these
        // rows - hence the explicit UpdatedAt stamp on every one.
        var movedTransactions = await db.Transactions
            .Where(t => t.UserId == userId && t.CategoryId == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.CategoryId, targetId)
                    .SetProperty(t => t.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

        var movedBudgets = await db.Budgets
            .Where(b => b.UserId == userId && b.CategoryId == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(b => b.CategoryId, (Guid?)targetId)
                    .SetProperty(b => b.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

        var movedRecurring = await db.RecurringTransactions
            .Where(r => r.UserId == userId && r.CategoryId == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.CategoryId, targetId)
                    .SetProperty(r => r.UpdatedAt, (DateTimeOffset?)now),
                cancellationToken);

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        await databaseTransaction.CommitAsync(cancellationToken);

        // Logged after the commit: an audit line for a rolled-back delete would
        // describe something that never happened.
        await audit.LogAsync(
            action: "category.deleted",
            userId: userId,
            entityName: nameof(Category),
            entityId: id,
            metadata: new
            {
                category.Name,
                reassignedTo = targetId,
                transactions = movedTransactions,
                budgets = movedBudgets,
                recurring = movedRecurring,
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Budgets are unique per (category, period, start date) while not deleted.
    /// Reassigning into a category that already caps the same window would break
    /// that index mid-transaction and surface to the user as a 500, so the
    /// collision is found first and explained in terms they can act on.
    /// </summary>
    private async Task GuardBudgetCollisionAsync(
        Guid userId,
        Guid sourceId,
        Guid targetId,
        string sourceName,
        string targetName,
        CancellationToken cancellationToken)
    {
        var collides = await db.Budgets
            .AsNoTracking()
            .Where(source => source.UserId == userId && source.CategoryId == sourceId)
            .AnyAsync(
                source => db.Budgets.Any(
                    existing => existing.UserId == userId
                        && existing.CategoryId == targetId
                        && existing.Period == source.Period
                        && existing.StartDate == source.StartDate),
                cancellationToken);

        if (collides)
        {
            throw new ConflictException(
                $"\"{sourceName}\" and \"{targetName}\" both have a budget covering the same period. " +
                "Remove one of those budgets, then delete the category.");
        }
    }

    private async Task<CategoryDto> ProjectAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .Where(category => category.Id == id && category.UserId == userId)
            .Select(CategoryMappings.ToDto(userId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Category), id);

    /// <summary>Appends to the end of the account's list for that ledger side, computed as a SQL MAX.</summary>
    private async Task<int> NextSortOrderAsync(Guid userId, TransactionType type, CancellationToken cancellationToken)
    {
        // Projected to int? so an empty set comes back as null rather than
        // throwing on a MAX over no rows.
        var highest = await db.Categories
            .AsNoTracking()
            .Where(category => category.UserId == userId && category.Type == type)
            .MaxAsync(category => (int?)category.SortOrder, cancellationToken);

        return (highest ?? 0) + DefaultCategories.SortOrderStep;
    }

    private static bool IsDuplicateName(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(UniqueNameIndex, StringComparison.Ordinal) == true;

    private static string DuplicateNameMessage(string name, TransactionType type) =>
        $"You already have an {Describe(type)} category called \"{name}\".";

    private static string Describe(TransactionType type) =>
        type == TransactionType.Expense ? "expense" : "income";

    private static string InUseMessage(string name, int transactions, int budgets, int recurring)
    {
        var parts = new List<string>(3);

        if (transactions > 0) parts.Add(Quantify(transactions, "transaction"));
        if (budgets > 0) parts.Add(Quantify(budgets, "budget"));
        if (recurring > 0) parts.Add(Quantify(recurring, "recurring entry", "recurring entries"));

        return $"\"{name}\" is still used by {JoinNaturally(parts)}. " +
            "Choose another category to move them to, then delete it again.";
    }

    private static string Quantify(int count, string singular, string? plural = null) =>
        count == 1 ? $"{count} {singular}" : $"{count} {plural ?? singular + "s"}";

    private static string JoinNaturally(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => "nothing",
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}",
    };
}
