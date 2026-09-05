using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Application.Features.Receipts;

namespace ExpenseManagement.Application.Features.Transactions;

/// <summary>
/// A receipt as it appears inside a transaction. Deliberately not the whole
/// <c>Receipt</c> row: the storage key is a server-side secret and putting it on
/// the wire would invite clients to build their own blob URLs.
/// </summary>
public sealed record ReceiptSummaryDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// An authorised API path, not a public URL — the bytes are still served
    /// behind the bearer token.
    ///
    /// Delegated to <see cref="ReceiptUrls.Content"/> rather than interpolated
    /// here. Two records model one wire type, and when both spelled the route
    /// out independently they drifted: this one said <c>/api/receipts/{id}/file</c>
    /// while the receipts feature served <c>/api/v1/receipts/{id}/content</c>.
    /// A thumbnail rendered from a fresh upload worked and the same thumbnail
    /// rendered from a refetched transaction 404'd, which reads as a flaky
    /// image rather than a wrong constant.
    /// </summary>
    public string Url => ReceiptUrls.Content(Id);
}

/// <summary>
/// The wire shape of a transaction. Category fields are flattened so the list
/// screen can render a row without a second lookup, and they are a snapshot of
/// the category as it is now — the category is the source of truth for its own
/// name and colour, unlike <see cref="CurrencyCode"/>, which is history.
/// </summary>
public sealed record TransactionDto(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    string CurrencyCode,
    Guid CategoryId,
    string CategoryName,
    string CategoryIcon,
    string CategoryColor,
    string? Description,
    string? Merchant,
    PaymentMethod PaymentMethod,
    DateTimeOffset TransactionDate,
    string? Notes,
    Guid? RecurringTransactionId,
    IReadOnlyList<ReceiptSummaryDto> Receipts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// The fields a create and an update have in common.
///
/// This exists so the service applies both through one code path: without it,
/// adding a field to <see cref="CreateTransactionRequest"/> and forgetting the
/// update path produces a field that can be set but never changed, which is the
/// kind of bug that only shows up in a user's data weeks later.
/// </summary>
public interface ITransactionWriteRequest
{
    TransactionType Type { get; }
    decimal Amount { get; }
    Guid CategoryId { get; }
    DateTimeOffset TransactionDate { get; }
    PaymentMethod PaymentMethod { get; }
    string? Description { get; }
    string? Merchant { get; }
    string? Notes { get; }

    /// <summary>Null means "use the account currency"; it never means INR.</summary>
    string? CurrencyCode { get; }
}

/// <summary>A new transaction, optionally carrying an offline idempotency key.</summary>
/// <param name="ClientReference">
/// Idempotency key for offline replay. A repeat of the same key updates the row
/// it created rather than adding a second one.
/// </param>
public sealed record CreateTransactionRequest(
    TransactionType Type,
    decimal Amount,
    Guid CategoryId,
    DateTimeOffset TransactionDate,
    PaymentMethod PaymentMethod,
    string? Description = null,
    string? Merchant = null,
    string? Notes = null,
    string? CurrencyCode = null,
    string? ClientReference = null) : ITransactionWriteRequest;

/// <summary>
/// Deliberately carries no <c>ClientReference</c>: the key identifies the row,
/// so letting an update rewrite it would let one queued edit steal another
/// device's idempotency slot.
/// </summary>
public sealed record UpdateTransactionRequest(
    TransactionType Type,
    decimal Amount,
    Guid CategoryId,
    DateTimeOffset TransactionDate,
    PaymentMethod PaymentMethod,
    string? Description = null,
    string? Merchant = null,
    string? Notes = null,
    string? CurrencyCode = null) : ITransactionWriteRequest;

/// <summary>
/// Filters for the transaction list. A class rather than a record because
/// <see cref="PaginationRequest"/> is a class and C# forbids a record deriving
/// from one — and because query-string model binding needs settable properties,
/// which is also what makes the base class's clamping setters work.
///
/// Every filter is nullable: absent must mean "do not filter", never "filter on
/// the default value".
/// </summary>
public sealed class TransactionQueryRequest : PaginationRequest
{
    public const string SortByDate = "date";
    public const string SortByAmount = "amount";
    public const string SortAscending = "asc";
    public const string SortDescending = "desc";

    /// <summary>Free text matched against merchant, description, notes and category name.</summary>
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public TransactionType? Type { get; set; }

    /// <summary>Inclusive lower bound, UTC.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Exclusive upper bound, UTC — so adjacent months tile with no overlap and no hole.</summary>
    public DateTimeOffset? To { get; set; }

    public decimal? MinAmount { get; set; }

    public decimal? MaxAmount { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary><see cref="SortByDate"/> or <see cref="SortByAmount"/>; date when omitted.</summary>
    public string? SortBy { get; set; }

    /// <summary><see cref="SortAscending"/> or <see cref="SortDescending"/>; descending when omitted.</summary>
    public string? SortDirection { get; set; }
}
