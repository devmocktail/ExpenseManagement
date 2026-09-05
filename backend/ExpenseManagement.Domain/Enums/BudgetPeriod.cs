namespace ExpenseManagement.Domain.Enums;

/// <summary>
/// The cadence a budget repeats on. <see cref="Custom"/> uses the explicit
/// start/end dates on the budget and never rolls forward.
/// </summary>
public enum BudgetPeriod : byte
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4,
    Custom = 99,
}
