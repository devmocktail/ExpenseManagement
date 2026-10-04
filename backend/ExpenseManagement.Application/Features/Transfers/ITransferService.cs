using ExpenseManagement.Application.Common.Models;

namespace ExpenseManagement.Application.Features.Transfers;

/// <summary>
/// Movements of money between two of the caller's own accounts.
///
/// Nothing here touches income or expenses: a transfer conserves money, so
/// every balance it changes is derived from these rows, and no total the user
/// sees as "spending" can ever include one.
/// </summary>
public interface ITransferService
{
    Task<PagedResult<TransferDto>> ListAsync(TransferQueryRequest query, CancellationToken cancellationToken);

    Task<TransferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<TransferDto> CreateAsync(CreateTransferRequest request, CancellationToken cancellationToken);

    Task<TransferDto> UpdateAsync(Guid id, UpdateTransferRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
