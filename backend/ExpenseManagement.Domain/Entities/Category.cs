using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// A spending or earning bucket. Every user gets their own copy of the default
/// set at registration, so editing or deleting one can never affect another
/// user and there is no shared-global-category coupling to unwind later.
/// </summary>
public class Category : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Which side of the ledger this category may be used on.</summary>
    public TransactionType Type { get; set; }

    /// <summary>Icon identifier resolved by the client's icon map (e.g. "food").</summary>
    public string Icon { get; set; } = "category";

    /// <summary>Hex colour, e.g. <c>#4F46E5</c>. Validated on write.</summary>
    public string Color { get; set; } = "#6366F1";

    /// <summary>
    /// True for the categories seeded at registration. System categories can be
    /// renamed and restyled but not deleted, guaranteeing every user always has
    /// somewhere to reassign transactions to.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>Display order in pickers; lower sorts first.</summary>
    public int SortOrder { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<RecurringTransaction> RecurringTransactions { get; set; } = [];
}
