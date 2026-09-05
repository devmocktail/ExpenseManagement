using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// A spending cap over a date window. A budget with a null <see cref="CategoryId"/>
/// is the overall budget for the period; one with a category caps that category alone.
/// </summary>
public class Budget : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Null means this is the overall budget for the period.</summary>
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "INR";

    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monthly;

    /// <summary>Inclusive UTC start of the window.</summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>Exclusive UTC end of the window — half-open [Start, End) avoids the classic end-of-day off-by-one.</summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>When true, a new window is opened automatically once this one closes.</summary>
    public bool IsRecurring { get; set; } = true;

    public bool IsActive { get; set; } = true;

    /// <summary>Per-budget override of the user's default warning percent. Null uses the account setting.</summary>
    public int? WarningThreshold { get; set; }
    public int? CriticalThreshold { get; set; }

    /// <summary>
    /// Highest alert percentage already notified for this window, so a user is
    /// not re-notified about 75% every time they add a coffee.
    /// </summary>
    public int? LastNotifiedThreshold { get; set; }

    public bool CoversInstant(DateTimeOffset instant) => instant >= StartDate && instant < EndDate;
}
