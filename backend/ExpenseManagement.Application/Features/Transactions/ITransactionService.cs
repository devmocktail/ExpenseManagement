using ExpenseManagement.Application.Common.Models;

namespace ExpenseManagement.Application.Features.Transactions;

/// <summary>
/// Reads and writes the caller's own transactions.
///
/// Every member resolves the user from the authenticated principal, never from
/// an argument, so there is no overload here that could be handed someone
/// else's id. A row that does not exist and a row that belongs to another
/// account both raise <c>NotFoundException</c>: answering 403 for the second
/// case would confirm the id is real and turn the API into an enumeration
/// oracle.
/// </summary>
public interface ITransactionService
{
    Task<PagedResult<TransactionDto>> ListAsync(
        TransactionQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a transaction, or — when the request carries a client reference
    /// this user has already used — updates the row that reference created and
    /// returns it, so replaying an offline queue cannot duplicate spending.
    /// </summary>
    Task<TransactionDto> CreateAsync(
        CreateTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> UpdateAsync(
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft deletes the transaction and tombstones its receipts with it.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
