using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Accounts;

/// <summary>
/// The account every new user starts with.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one, and deliberately so. Seeding a plausible set — "HDFC Savings",
/// "Paytm Wallet" — would put accounts in the list that the user does not have,
/// and an expense filed against a fictional account is worse than one filed
/// against none.
/// </para>
/// <para>
/// Cash is the safe default because it is the one pot everybody genuinely has,
/// it needs no details to be true, and it is where someone who never opens the
/// accounts screen will still correctly end up. Their real bank accounts are
/// theirs to add.
/// </para>
/// </remarks>
public static class DefaultAccounts
{
    public sealed record Template(string Name, AccountType Type, string Icon, string Color, int SortOrder);

    public static readonly IReadOnlyList<Template> All =
    [
        new("Cash in hand", AccountType.Cash, "wallet-outline", "#16A34A", 0),
    ];
}
