using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Transfers;

/// <inheritdoc cref="ITransferService"/>
public sealed class TransferService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ITransferService
{
    private const string UniqueClientReferenceIndex = "UX_Transfers_UserId_ClientReference";

    public async Task<PagedResult<TransferDto>> ListAsync(
        TransferQueryRequest query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var transfers = db.Transfers
            .AsNoTracking()
            .Where(transfer => transfer.UserId == userId);

        if (query.AccountId is { } accountId)
        {
            transfers = transfers.Where(
                transfer => transfer.FromAccountId == accountId || transfer.ToAccountId == accountId);
        }

        if (query.From is { } from)
        {
            transfers = transfers.Where(transfer => transfer.TransferDate >= from);
        }

        // Exclusive, so a caller asking for March and a caller asking for April
        // never both see a transfer stamped exactly at midnight on the 1st.
        if (query.To is { } to)
        {
            transfers = transfers.Where(transfer => transfer.TransferDate < to);
        }

        var total = await transfers.CountAsync(cancellationToken);

        if (total == 0)
        {
            return PagedResult<TransferDto>.Empty(query.Page, query.PageSize);
        }

        var items = await transfers
            .OrderByDescending(transfer => transfer.TransferDate)
            .ThenByDescending(transfer => transfer.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(TransferMappings.ToDto())
            .ToListAsync(cancellationToken);

        return new PagedResult<TransferDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total,
        };
    }

    public async Task<TransferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        return await ProjectAsync(id, userId, cancellationToken);
    }

    public async Task<TransferDto> CreateAsync(
        CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var (from, to) = await LoadBothEndsAsync(
            userId, request.FromAccountId, request.ToAccountId, cancellationToken);

        var transfer = new Transfer
        {
            UserId = userId,
            FromAccountId = from.Id,
            ToAccountId = to.Id,
            Amount = request.Amount,
            // Taken from the accounts, never from the request. The two agree by
            // the time we get here, and a client-supplied code could disagree
            // with both.
            CurrencyCode = from.CurrencyCode,
            TransferDate = request.TransferDate,
            Notes = request.Notes?.Trim(),
            ClientReference = request.ClientReference?.Trim(),
        };

        db.Transfers.Add(transfer);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateClientReference(ex))
        {
            // A retried upload after a dropped connection. Returning the
            // transfer that already landed is the whole point of the key —
            // moving the money a second time would be the actual failure.
            var existing = await db.Transfers
                .AsNoTracking()
                .Where(t => t.UserId == userId && t.ClientReference == transfer.ClientReference)
                .Select(TransferMappings.ToDto())
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null) return existing;
            throw;
        }

        return await ProjectAsync(transfer.Id, userId, cancellationToken);
    }

    public async Task<TransferDto> UpdateAsync(
        Guid id,
        UpdateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var transfer = await db.Transfers
            .Where(t => t.Id == id && t.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Transfer), id);

        var (from, to) = await LoadBothEndsAsync(
            userId, request.FromAccountId, request.ToAccountId, cancellationToken);

        transfer.FromAccountId = from.Id;
        transfer.ToAccountId = to.Id;
        transfer.Amount = request.Amount;
        transfer.CurrencyCode = from.CurrencyCode;
        transfer.TransferDate = request.TransferDate;
        transfer.Notes = request.Notes?.Trim();
        transfer.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return await ProjectAsync(id, userId, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var transfer = await db.Transfers
            .Where(t => t.Id == id && t.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Transfer), id);

        transfer.IsDeleted = true;
        transfer.DeletedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Resolves both ends and enforces everything that cannot be checked from
    /// the request alone.
    /// </summary>
    /// <remarks>
    /// Each account is looked up with its own ownership predicate, so an id
    /// belonging to someone else is a 404 exactly like an id that does not
    /// exist — a 403 would confirm the account is real, which is the oracle
    /// this whole codebase avoids.
    /// </remarks>
    private async Task<(Account From, Account To)> LoadBothEndsAsync(
        Guid userId,
        Guid fromAccountId,
        Guid toAccountId,
        CancellationToken cancellationToken)
    {
        // Checked before the round trips: the database rejects this too, via
        // CK_Transfers_Accounts_Differ, but a constraint violation surfaces as a
        // 500 rather than as something the user can act on.
        if (fromAccountId == toAccountId)
        {
            throw new BusinessRuleException(
                "A transfer needs two different accounts.",
                "transfer_same_account");
        }

        var accounts = await db.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId && (a.Id == fromAccountId || a.Id == toAccountId))
            .ToListAsync(cancellationToken);

        var from = accounts.FirstOrDefault(a => a.Id == fromAccountId)
            ?? throw new NotFoundException(nameof(Account), fromAccountId);

        var to = accounts.FirstOrDefault(a => a.Id == toAccountId)
            ?? throw new NotFoundException(nameof(Account), toAccountId);

        // Converting would need a rate, and inventing one would manufacture or
        // destroy money: the amount leaving one account would not be the amount
        // arriving in the other, and the two balances would disagree for ever.
        if (!string.Equals(from.CurrencyCode, to.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                $"\"{from.Name}\" is in {from.CurrencyCode} and \"{to.Name}\" is in {to.CurrencyCode}. "
                + "Transfers between currencies are not supported yet.",
                "transfer_currency_mismatch");
        }

        // An archived account is hidden from the pickers, so a transfer naming
        // one is a stale client — and letting it through would move money into
        // a pot the user believes is closed.
        if (from.IsArchived || to.IsArchived)
        {
            var archived = from.IsArchived ? from.Name : to.Name;
            throw new BusinessRuleException(
                $"\"{archived}\" is archived. Unarchive it before moving money through it.",
                "transfer_archived_account");
        }

        return (from, to);
    }

    private async Task<TransferDto> ProjectAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        await db.Transfers
            .AsNoTracking()
            .Where(transfer => transfer.Id == id && transfer.UserId == userId)
            .Select(TransferMappings.ToDto())
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Transfer), id);

    private static bool IsDuplicateClientReference(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(UniqueClientReferenceIndex, StringComparison.Ordinal) == true;
}
