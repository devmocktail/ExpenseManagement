using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Categories;

/// <summary>
/// One entry of the starter set every account is seeded with at registration.
/// A template, not an entity: the rows themselves are per-user copies, so one
/// account renaming "Food" to "Eating out" can never be visible to another.
/// </summary>
public sealed record DefaultCategory(string Name, TransactionType Type, string Icon, string Color, int SortOrder);

/// <summary>
/// The categories a brand-new account starts with, seeded by the registration
/// flow. Every one of them is created with <c>IsSystem = true</c>, which makes
/// it undeletable — that is the guarantee that a user always has somewhere to
/// reassign transactions to when they delete a category of their own.
/// </summary>
public static class DefaultCategories
{
    /// <summary>
    /// Gap left between consecutive sort orders. Users reorder by dragging, and
    /// a new category lands at the end on the same stride, so a future reorder
    /// can slot a row between two others without renumbering the whole list.
    /// </summary>
    public const int SortOrderStep = 10;

    /// <summary>
    /// Income sort orders start well past the expense block so that a combined
    /// list — <c>ListAsync(type: null)</c> orders by sort order, then name —
    /// still shows all spending buckets before all earning ones.
    /// </summary>
    private const int IncomeSortOrderBase = 200;

    /// <summary>
    /// Icons are lowercase slugs resolved by the client's icon map; colours are
    /// exactly <c>#RRGGBB</c> because the column is a fixed-width <c>nchar(7)</c>
    /// with a check constraint, and each is distinct so two chips in the same
    /// chart legend are never the same swatch.
    /// </summary>
    public static IReadOnlyList<DefaultCategory> All { get; } =
    [
        new("Food",          TransactionType.Expense, "food",            "#F97316", SortOrderStep * 1),
        new("Groceries",     TransactionType.Expense, "shopping-cart",   "#22C55E", SortOrderStep * 2),
        new("Transport",     TransactionType.Expense, "car",             "#3B82F6", SortOrderStep * 3),
        new("Shopping",      TransactionType.Expense, "shopping-bag",    "#EC4899", SortOrderStep * 4),
        new("Bills",         TransactionType.Expense, "receipt",         "#EF4444", SortOrderStep * 5),
        new("Rent",          TransactionType.Expense, "home",            "#8B5CF6", SortOrderStep * 6),
        new("Utilities",     TransactionType.Expense, "bolt",            "#EAB308", SortOrderStep * 7),
        new("Entertainment", TransactionType.Expense, "film",            "#A855F7", SortOrderStep * 8),
        new("Healthcare",    TransactionType.Expense, "heart-pulse",     "#14B8A6", SortOrderStep * 9),
        new("Education",     TransactionType.Expense, "book-open",       "#0EA5E9", SortOrderStep * 10),
        new("Travel",        TransactionType.Expense, "plane",           "#06B6D4", SortOrderStep * 11),
        new("Subscriptions", TransactionType.Expense, "repeat",          "#6366F1", SortOrderStep * 12),
        new("Personal Care", TransactionType.Expense, "sparkles",        "#F472B6", SortOrderStep * 13),
        new("Other",         TransactionType.Expense, "dots-horizontal", "#94A3B8", SortOrderStep * 14),

        new("Salary",        TransactionType.Income,  "wallet",          "#16A34A", IncomeSortOrderBase + SortOrderStep * 1),
        new("Freelance",     TransactionType.Income,  "laptop",          "#2563EB", IncomeSortOrderBase + SortOrderStep * 2),
        new("Business",      TransactionType.Income,  "briefcase",       "#7C3AED", IncomeSortOrderBase + SortOrderStep * 3),
        new("Investment",    TransactionType.Income,  "trending-up",     "#0D9488", IncomeSortOrderBase + SortOrderStep * 4),
        new("Gift",          TransactionType.Income,  "gift",            "#DB2777", IncomeSortOrderBase + SortOrderStep * 5),
        new("Other",         TransactionType.Income,  "dots-horizontal", "#64748B", IncomeSortOrderBase + SortOrderStep * 6),
    ];

    /// <summary>
    /// Materialises the starter set as unsaved <see cref="Category"/> rows for
    /// one account. Ids are left unset for the database's
    /// <c>NEWSEQUENTIALID()</c> default and timestamps for the SaveChanges
    /// interceptor — assigning either here would fight the infrastructure that
    /// already owns them.
    /// </summary>
    public static IReadOnlyList<Category> CreateFor(Guid userId) =>
    [
        .. All.Select(template => new Category
        {
            UserId = userId,
            Name = template.Name,
            Type = template.Type,
            Icon = template.Icon,
            Color = template.Color,
            SortOrder = template.SortOrder,
            IsSystem = true,
        })
    ];
}
