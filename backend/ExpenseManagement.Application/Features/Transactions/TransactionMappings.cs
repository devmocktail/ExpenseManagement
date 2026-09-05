using System.Linq.Expressions;
using ExpenseManagement.Domain.Entities;

namespace ExpenseManagement.Application.Features.Transactions;

/// <summary>
/// The one projection every transaction read goes through.
///
/// Sharing a single <see cref="Expression"/> between the list and the detail
/// query is what keeps them honest: a field added here appears in both, and
/// neither can drift into loading whole entities and mapping them afterwards.
/// </summary>
public static class TransactionMappings
{
    /// <summary>
    /// Held in a get-only property so the tree is built once per process rather
    /// than per request; EF caches its compiled plan by shape either way, but
    /// there is no reason to re-allocate the tree on every call.
    /// </summary>
    public static Expression<Func<Transaction, TransactionDto>> ToDto { get; } = t => new TransactionDto(
        t.Id,
        t.Type,
        t.Amount,
        t.CurrencyCode,
        t.CategoryId,
        t.Category.Name,
        t.Category.Icon,
        t.Category.Color,
        t.Description,
        t.Merchant,
        t.PaymentMethod,
        t.TransactionDate,
        t.Notes,
        t.RecurringTransactionId,

        // Receipts are reached through the owning transaction, which the caller
        // has already constrained to the authenticated user, so no second
        // ownership predicate is needed here — and the soft-delete filter still
        // applies to the navigation, so tombstoned attachments stay hidden.
        // Ordered by creation so the attachment strip does not reshuffle itself
        // between two loads of the same screen.
        t.Receipts
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Select(r => new ReceiptSummaryDto(
                r.Id,
                r.FileName,
                r.ContentType,
                r.FileSizeBytes,
                r.CreatedAt))
            .ToList(),

        t.CreatedAt,
        t.UpdatedAt);
}
