namespace ExpenseManagement.Domain.Enums;

/// <summary>
/// What kind of pot the money sits in. Persisted as <c>tinyint</c>.
///
/// This is deliberately not the same thing as <see cref="PaymentMethod"/>.
/// PaymentMethod records how a single transaction was settled ("I tapped a
/// card"); AccountType describes a durable balance that transfers move money
/// between ("my HDFC current account"). A UPI payment and a card payment can
/// both come out of the same bank account.
/// </summary>
public enum AccountType : byte
{
    /// <summary>Physical notes and coins — the "in hand" pot.</summary>
    Cash = 1,

    /// <summary>A current or savings account at a bank.</summary>
    Bank = 2,

    /// <summary>
    /// A card with a credit limit. Its balance is normally negative: money
    /// spent is money owed, and paying the bill is a transfer from a bank
    /// account into the card, which moves the balance back towards zero.
    /// </summary>
    CreditCard = 3,

    /// <summary>A prepaid wallet such as Paytm or PhonePe.</summary>
    Wallet = 4,

    /// <summary>Money set aside — a fixed deposit, a savings goal, a piggy bank.</summary>
    Savings = 5,

    Other = 99,
}
