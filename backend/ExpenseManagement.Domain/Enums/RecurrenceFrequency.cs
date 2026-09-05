namespace ExpenseManagement.Domain.Enums;

/// <summary>How often a recurring transaction generates a real transaction.</summary>
public enum RecurrenceFrequency : byte
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4,
}
