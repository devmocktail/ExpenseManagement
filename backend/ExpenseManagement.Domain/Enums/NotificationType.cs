namespace ExpenseManagement.Domain.Enums;

public enum NotificationType : byte
{
    BudgetWarning = 1,
    BudgetExceeded = 2,
    RecurringDue = 3,
    MonthlySummary = 4,
    System = 99,
}
