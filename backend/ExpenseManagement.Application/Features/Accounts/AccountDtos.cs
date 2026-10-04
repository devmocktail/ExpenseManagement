using System.Linq.Expressions;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Accounts;

/// <summary>
/// An account as the mobile client sees it. Mirrors <c>Account</c> in
/// <c>mobile/src/types/api.ts</c> field for field.
/// </summary>
/// <param name="Balance">
/// Derived, never stored: opening balance, plus income into this account, minus
/// expenses from it, plus transfers in, minus transfers out. Computed by the
/// database on every read so it cannot drift from the rows it summarises.
/// </param>
/// <param name="TransactionCount">
/// Live count of transactions against this account. The client shows it in the
/// delete confirmation, so "this account has 42 entries" is answered before the
/// request is sent rather than after it is refused.
/// </param>
public sealed record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string CurrencyCode,
    decimal OpeningBalance,
    decimal Balance,
    string? Institution,
    string? Last4,
    string Icon,
    string Color,
    bool IsDefault,
    bool IsArchived,
    int SortOrder,
    int TransactionCount,
    int TransferCount);

public sealed record CreateAccountRequest(
    string Name,
    AccountType Type,
    string? CurrencyCode,
    decimal? OpeningBalance,
    string? Institution,
    string? Last4,
    string? Icon,
    string? Color,
    bool? IsDefault,
    int? SortOrder);

/// <summary>
/// Edit of an existing account.
///
/// <c>CurrencyCode</c> is deliberately absent. Changing it would reinterpret
/// every amount already recorded against the account — ₹50,000 of history
/// silently becoming $50,000 — without touching a single row. Moving to a
/// different currency is a new account, not a rename.
/// </summary>
public sealed record UpdateAccountRequest(
    string Name,
    AccountType Type,
    decimal? OpeningBalance,
    string? Institution,
    string? Last4,
    string? Icon,
    string? Color,
    bool? IsArchived,
    int? SortOrder);

public static class AccountMappings
{
    /// <summary>
    /// The single definition of how an <see cref="Account"/> becomes an
    /// <see cref="AccountDto"/>, shared by the list and single-row queries so
    /// the balance can never be computed two different ways.
    /// </summary>
    /// <remarks>
    /// Every aggregate is cast to <c>decimal?</c> before summing. SQL's SUM
    /// returns NULL over an empty set, not zero, and without the cast EF
    /// materialises that NULL into a non-nullable decimal and throws on the
    /// first account that has no activity yet — which is every account on the
    /// day it is created.
    ///
    /// Built per caller rather than as a static expression so the correlated
    /// subqueries carry the same explicit ownership predicate as the outer
    /// query, instead of leaning solely on the DbContext's global filter. They
    /// stay subqueries in SQL; no rows are pulled back to be summed here.
    /// </remarks>
    public static Expression<Func<Account, AccountDto>> ToDto(Guid userId) =>
        account => new AccountDto(
            account.Id,
            account.Name,
            account.Type,
            account.CurrencyCode,
            account.OpeningBalance,
            account.OpeningBalance
                + (account.Transactions
                    .Where(t => t.UserId == userId && t.Type == TransactionType.Income)
                    .Sum(t => (decimal?)t.Amount) ?? 0m)
                - (account.Transactions
                    .Where(t => t.UserId == userId && t.Type == TransactionType.Expense)
                    .Sum(t => (decimal?)t.Amount) ?? 0m)
                + (account.TransfersIn
                    .Where(t => t.UserId == userId)
                    .Sum(t => (decimal?)t.Amount) ?? 0m)
                - (account.TransfersOut
                    .Where(t => t.UserId == userId)
                    .Sum(t => (decimal?)t.Amount) ?? 0m),
            account.Institution,
            account.Last4,
            account.Icon,
            account.Color,
            account.IsDefault,
            account.IsArchived,
            account.SortOrder,
            account.Transactions.Count(t => t.UserId == userId),
            account.TransfersIn.Count(t => t.UserId == userId)
                + account.TransfersOut.Count(t => t.UserId == userId));
}
