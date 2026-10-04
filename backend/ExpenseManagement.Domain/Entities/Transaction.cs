using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// A single expense or income entry — the core row of the product.
/// Money is <see cref="decimal"/> end to end and stored as <c>decimal(18,2)</c>;
/// floating point is never used for currency anywhere in this system.
/// </summary>
public class Transaction : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Which pot the money came from or went into.
    ///
    /// Nullable, and intentionally so. Every row written before accounts
    /// existed has none, and inventing an account for them would attribute
    /// spending to a balance the user never chose. An unassigned transaction
    /// still counts towards income, expenses and budgets — it simply moves no
    /// account balance, which is the honest answer when we do not know which
    /// one it touched.
    /// </summary>
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>Always a positive magnitude; direction is carried by <see cref="Type"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 code captured at write time. Denormalised onto the row so that
    /// changing the account currency later cannot silently rewrite history.
    /// </summary>
    public string CurrencyCode { get; set; } = "INR";

    public string? Description { get; set; }
    public string? Merchant { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>Instant the money moved, in UTC. Rendered in the user's zone by the client.</summary>
    public DateTimeOffset TransactionDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>Set when this row was generated from a schedule rather than entered by hand.</summary>
    public Guid? RecurringTransactionId { get; set; }
    public RecurringTransaction? RecurringTransaction { get; set; }

    /// <summary>
    /// Client-supplied idempotency key for offline sync. Unique per user, so a
    /// retried upload after a dropped connection updates rather than duplicates.
    /// </summary>
    public string? ClientReference { get; set; }

    public ICollection<Receipt> Receipts { get; set; } = [];

    /// <summary>Signed contribution to a balance: income adds, expense subtracts.</summary>
    public decimal SignedAmount => Type == TransactionType.Income ? Amount : -Amount;
}
