namespace ExpenseManagement.Application.Features.Accounts;

/// <summary>
/// Accounts: the pots money sits in. Every method is scoped to the caller by
/// the DbContext's ownership filter, so "not found" and "not yours" are
/// indistinguishable from the outside — returning 403 for another user's
/// account would confirm the id exists.
/// </summary>
public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);

    Task<AccountDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken);

    Task<AccountDto> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes an account that nothing references. An account with history
    /// is refused — archive it instead, which hides it from pickers while
    /// leaving what it already paid for intact.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Makes this the account pre-selected when adding a transaction.</summary>
    Task<AccountDto> SetDefaultAsync(Guid id, CancellationToken cancellationToken);
}
