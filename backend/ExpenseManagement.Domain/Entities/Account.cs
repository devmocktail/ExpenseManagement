using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// A pot of money the user owns: a bank account, a card, a wallet, or the cash
/// in their pocket. Transactions are spent from one and transfers move money
/// between two.
/// </summary>
/// <remarks>
/// <para>
/// The balance is <em>derived</em>, never stored on the row:
/// <c>OpeningBalance + Σ signed transactions + Σ transfers in − Σ transfers out</c>.
/// A stored running balance is a second source of truth, and it drifts the
/// first time a write half-fails or a row is edited by anything that forgets to
/// adjust it. Deriving it costs an aggregate query and can never be wrong.
/// </para>
/// </remarks>
public class Account : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    /// <summary>What the user calls it — "HDFC Savings", "Wallet", "Cash in hand".</summary>
    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; }

    /// <summary>
    /// ISO 4217. Transfers are only permitted between accounts sharing a
    /// currency: converting would need a rate, and inventing one would silently
    /// manufacture or destroy money.
    /// </summary>
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>
    /// What the account held before the first recorded transaction, so someone
    /// starting mid-life does not have to back-enter their history to see a
    /// true balance. May be negative, which is the normal state of a credit card.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Bank or provider name, for telling two similar accounts apart.</summary>
    public string? Institution { get; set; }

    /// <summary>
    /// The last four digits only, and only as a label.
    ///
    /// A full account or card number is never stored: it would turn a read-only
    /// data leak into a payment credential, it is not needed to do anything this
    /// app does, and holding card numbers at all drags the system into PCI DSS
    /// scope for no benefit.
    /// </summary>
    public string? Last4 { get; set; }

    public string Icon { get; set; } = "wallet-outline";

    /// <summary>Exactly <c>#RRGGBB</c>; the client renders swatches straight from it.</summary>
    public string Color { get; set; } = "#4F46E5";

    /// <summary>
    /// Pre-selected when adding a transaction. Exactly one account per user
    /// carries this, enforced by a filtered unique index.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Hidden from pickers but still counted in history. Closing a real account
    /// must not rewrite what it already paid for, so archiving is the
    /// alternative to deleting.
    /// </summary>
    public bool IsArchived { get; set; }

    public int SortOrder { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<Transfer> TransfersOut { get; set; } = [];
    public ICollection<Transfer> TransfersIn { get; set; } = [];
}
