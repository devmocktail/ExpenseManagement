using System.Linq.Expressions;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Transfers;

/// <summary>
/// A transfer as the mobile client sees it. Both ends are denormalised into
/// the row — name, type, icon and colour for each account — because the
/// transfers list renders "HDFC Savings → Cash in hand" on every line, and
/// resolving that client-side would mean either a second request or a lookup
/// table the client has to keep in step.
/// </summary>
public sealed record TransferDto(
    Guid Id,
    Guid FromAccountId,
    string FromAccountName,
    AccountType FromAccountType,
    string FromAccountIcon,
    string FromAccountColor,
    Guid ToAccountId,
    string ToAccountName,
    AccountType ToAccountType,
    string ToAccountIcon,
    string ToAccountColor,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset TransferDate,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTimeOffset TransferDate,
    string? Notes,
    string? ClientReference);

public sealed record UpdateTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTimeOffset TransferDate,
    string? Notes);

/// <summary>Filters for the transfers list.</summary>
public sealed class TransferQueryRequest : PaginationRequest
{
    /// <summary>
    /// Matches a transfer with this account on <em>either</em> end. Asking "what
    /// moved in and out of my wallet" is one question, not two.
    /// </summary>
    public Guid? AccountId { get; set; }

    /// <summary>Inclusive lower bound, UTC.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Exclusive upper bound, UTC — so adjacent months tile with no overlap and no hole.</summary>
    public DateTimeOffset? To { get; set; }
}

public static class TransferMappings
{
    /// <summary>
    /// The single definition of how a <see cref="Transfer"/> becomes a
    /// <see cref="TransferDto"/>, shared by the list and single-row queries.
    /// The two account navigations become joins, not extra round trips.
    /// </summary>
    public static Expression<Func<Transfer, TransferDto>> ToDto() =>
        transfer => new TransferDto(
            transfer.Id,
            transfer.FromAccountId,
            transfer.FromAccount.Name,
            transfer.FromAccount.Type,
            transfer.FromAccount.Icon,
            transfer.FromAccount.Color,
            transfer.ToAccountId,
            transfer.ToAccount.Name,
            transfer.ToAccount.Type,
            transfer.ToAccount.Icon,
            transfer.ToAccount.Color,
            transfer.Amount,
            transfer.CurrencyCode,
            transfer.TransferDate,
            transfer.Notes,
            transfer.CreatedAt,
            transfer.UpdatedAt);
}
