namespace ExpenseManagement.Domain.Enums;

/// <summary>Direction of money movement. Persisted as <c>tinyint</c>.</summary>
public enum TransactionType : byte
{
    Expense = 1,
    Income = 2,
}
