using ExpenseManagement.Domain.Common;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// Money moved between two of the user's own accounts — bank to wallet, bank to
/// cash in hand, a card bill paid from a current account.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately <strong>not</strong> a <see cref="Transaction"/>, and
/// that is the single most important thing about the type.
/// </para>
/// <para>
/// Withdrawing ₹10,000 from a bank is not ₹10,000 of spending — the user is no
/// poorer afterwards. Were transfers stored as transactions, every sum of
/// income or expenses in the system would have to remember to exclude them, and
/// the first query that forgot would quietly overstate what the user spends.
/// Transfers live in their own table so that exclusion is structural rather
/// than remembered: the dashboard, the analytics summary and the budget
/// evaluator all read <c>Transactions</c>, so none of them can see a transfer
/// even by accident.
/// </para>
/// <para>
/// A transfer therefore conserves money exactly. There is no fee field: a bank
/// charge is real spending that leaves the system entirely, so it belongs in
/// <c>Transactions</c> as an expense against the source account. Netting it off
/// here would make the pair of balances disagree with the amount moved.
/// </para>
/// </remarks>
public class Transfer : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    /// <summary>The account the money leaves. Must differ from <see cref="ToAccountId"/>.</summary>
    public Guid FromAccountId { get; set; }
    public Account FromAccount { get; set; } = null!;

    /// <summary>The account the money arrives in.</summary>
    public Guid ToAccountId { get; set; }
    public Account ToAccount { get; set; } = null!;

    /// <summary>Always positive. Direction is carried by the two account ids, not by a sign.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Captured at write time from the accounts involved, which must agree.
    /// Denormalised so that renaming an account's currency later cannot rewrite
    /// what already happened.
    /// </summary>
    public string CurrencyCode { get; set; } = "INR";

    /// <summary>Instant the money moved, in UTC.</summary>
    public DateTimeOffset TransferDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Client-supplied idempotency key for offline sync, unique per user — a
    /// retried upload after a dropped connection updates rather than
    /// duplicating, which on a transfer would otherwise move the money twice.
    /// </summary>
    public string? ClientReference { get; set; }
}
