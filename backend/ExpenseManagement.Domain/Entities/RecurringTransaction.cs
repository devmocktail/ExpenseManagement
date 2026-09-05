using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// A schedule that materialises real <see cref="Transaction"/> rows — rent,
/// subscriptions, EMIs. The schedule itself is never counted in totals; only
/// the transactions it generates are, so analytics can't double-count.
/// </summary>
public class RecurringTransaction : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public TransactionType Type { get; set; } = TransactionType.Expense;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "INR";

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Merchant { get; set; }
    public string? Description { get; set; }

    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

    /// <summary>Every <c>Interval</c> units of <see cref="Frequency"/> — 2 + Weekly = fortnightly.</summary>
    public int Interval { get; set; } = 1;

    public DateTimeOffset StartDate { get; set; }

    /// <summary>Null runs indefinitely until paused or deleted.</summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>
    /// UTC instant the next occurrence is due. Advanced only after a transaction
    /// is committed, so a crash mid-run repeats rather than skips — and the
    /// unique client reference on the generated row makes the repeat a no-op.
    /// </summary>
    public DateTimeOffset NextRunDate { get; set; }

    public DateTimeOffset? LastRunDate { get; set; }
    public int OccurrencesGenerated { get; set; }

    /// <summary>Paused schedules keep their state but generate nothing.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Days before the due date to send a reminder. 0 disables the reminder.</summary>
    public int ReminderDaysBefore { get; set; } = 1;

    public ICollection<Transaction> GeneratedTransactions { get; set; } = [];

    public bool IsDueAt(DateTimeOffset now) =>
        !IsPaused && NextRunDate <= now && (EndDate is null || NextRunDate <= EndDate);
}
