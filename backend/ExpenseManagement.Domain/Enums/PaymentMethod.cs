namespace ExpenseManagement.Domain.Enums;

/// <summary>How a transaction was settled. Persisted as <c>tinyint</c>.</summary>
public enum PaymentMethod : byte
{
    Cash = 1,
    CreditCard = 2,
    DebitCard = 3,
    Upi = 4,
    BankTransfer = 5,
    Wallet = 6,
    Other = 99,
}
