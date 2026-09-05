using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Transactions;

/// <inheritdoc cref="ITransactionService"/>
public sealed class TransactionService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : ITransactionService
{
    /// <summary>
    /// Backslash, passed through to SQL as ESCAPE. T-SQL has no default escape
    /// character, so without one a user searching for "50%" or for "credit_card"
    /// silently gets a wildcard query instead of a literal match.
    /// </summary>
    private const string LikeEscape = "\\";

    public async Task<PagedResult<TransactionDto>> ListAsync(
        TransactionQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        // The context's ownership filter would scope this anyway; the explicit
        // predicate states the intent and still holds if an IgnoreQueryFilters
        // call is ever added upstream.
        var query = db.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        query = ApplyFilters(query, request);

        var totalCount = await query.CountAsync(cancellationToken);

        // Nothing matched, so skip the second round trip: the page query would
        // only be a more expensive way to reach the same empty answer.
        if (totalCount == 0)
        {
            return PagedResult<TransactionDto>.Empty(request.Page, request.PageSize);
        }

        var items = await ApplyOrdering(query, request)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(TransactionMappings.ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<TransactionDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<TransactionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        return await db.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == id)
            .Select(TransactionMappings.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Transaction", id);
    }

    public async Task<TransactionDto> CreateAsync(
        CreateTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var currencyCode = await ValidateCategoryAndResolveCurrencyAsync(userId, request, cancellationToken);
        var clientReference = Normalise(request.ClientReference);

        if (clientReference is not null)
        {
            // Tracked on purpose: a hit here is an update, not a read.
            var replayed = await db.Transactions
                .Where(x => x.UserId == userId && x.ClientReference == clientReference)
                .FirstOrDefaultAsync(cancellationToken);

            // Offline replay has to converge on a single row. The unique index
            // behind this is filtered on IsDeleted = 0, so a reference whose row
            // the user has since deleted falls through to an insert instead of
            // quietly resurrecting a transaction they meant to be rid of.
            if (replayed is not null)
            {
                Apply(replayed, request, currencyCode);
                await db.SaveChangesAsync(cancellationToken);

                return await GetByIdAsync(replayed.Id, cancellationToken);
            }
        }

        var entity = new Transaction
        {
            UserId = userId,
            ClientReference = clientReference,
        };

        Apply(entity, request, currencyCode);
        db.Transactions.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (clientReference is not null)
        {
            // Two devices flushing the same queue at once both miss the lookup
            // above and race to insert; the filtered unique index picks a winner.
            // The loser reports the winner's row, because a sync the user never
            // triggered by hand must not surface as an error they cannot act on.
            // Any failure that is not that collision still rethrows.
            var winner = await db.Transactions
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.ClientReference == clientReference)
                .Select(TransactionMappings.ToDto)
                .FirstOrDefaultAsync(cancellationToken);

            if (winner is null)
            {
                throw;
            }

            return winner;
        }

        // Re-read rather than hand-build the response: the category columns and
        // receipts the DTO flattens are not on the tracked entity, and going back
        // through the shared projection guarantees a created row and a listed row
        // are described identically.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TransactionDto> UpdateAsync(
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var entity = await db.Transactions
            .Where(x => x.UserId == userId && x.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Transaction", id);

        var currencyCode = await ValidateCategoryAndResolveCurrencyAsync(userId, request, cancellationToken);

        Apply(entity, request, currencyCode);
        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var entity = await db.Transactions
            .Where(x => x.UserId == userId && x.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Transaction", id);

        // A soft delete is an UPDATE, so the database's Transaction to Receipt
        // cascade never fires. Tombstoning the attachments in the same unit of
        // work stops them lingering as live rows pinning blobs that the retention
        // sweep would then never reclaim.
        var receipts = await db.Receipts
            .Where(r => r.UserId == userId && r.TransactionId == id)
            .ToListAsync(cancellationToken);

        db.Receipts.RemoveRange(receipts);
        db.Transactions.Remove(entity);

        await db.SaveChangesAsync(cancellationToken);

        // Money leaving the ledger is worth a trail even though the row itself is
        // recoverable; the amount is captured here because the tombstone can be
        // purged by retention long before anyone asks what was removed.
        await audit.LogAsync(
            "transaction.deleted",
            userId,
            nameof(Transaction),
            id,
            metadata: new { entity.Amount, entity.CurrencyCode, entity.TransactionDate },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Attaches only the filters the caller actually supplied. Composing them
    /// unconditionally is the classic bug: an omitted search becomes a match on
    /// "%%" and an omitted category becomes a comparison against
    /// <see cref="Guid.Empty"/>, both of which change the result set for a
    /// parameter the client never sent.
    /// </summary>
    private static IQueryable<Transaction> ApplyFilters(
        IQueryable<Transaction> query,
        TransactionQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = ToLikePattern(request.Search);

            // Case-insensitivity comes from the column collation, not from
            // ToLower(): wrapping the column in a function makes the predicate
            // non-sargable and turns every search into a scan.
            query = query.Where(x =>
                EF.Functions.Like(x.Merchant!, pattern, LikeEscape) ||
                EF.Functions.Like(x.Description!, pattern, LikeEscape) ||
                EF.Functions.Like(x.Notes!, pattern, LikeEscape) ||
                EF.Functions.Like(x.Category.Name, pattern, LikeEscape));
        }

        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        if (request.Type is { } type)
        {
            query = query.Where(x => x.Type == type);
        }

        // Half-open [From, To). An inclusive upper bound would force the caller to
        // invent an end-of-day sentinel, and would double-count the boundary
        // instant whenever two adjacent ranges are requested back to back.
        if (request.From is { } from)
        {
            query = query.Where(x => x.TransactionDate >= from);
        }

        if (request.To is { } to)
        {
            query = query.Where(x => x.TransactionDate < to);
        }

        if (request.MinAmount is { } minAmount)
        {
            query = query.Where(x => x.Amount >= minAmount);
        }

        if (request.MaxAmount is { } maxAmount)
        {
            query = query.Where(x => x.Amount <= maxAmount);
        }

        if (request.PaymentMethod is { } paymentMethod)
        {
            query = query.Where(x => x.PaymentMethod == paymentMethod);
        }

        return query;
    }

    /// <summary>
    /// Newest first by default, which is both what the list screen shows and the
    /// direction of the covering index on (UserId, TransactionDate DESC).
    /// </summary>
    private static IQueryable<Transaction> ApplyOrdering(
        IQueryable<Transaction> query,
        TransactionQueryRequest request)
    {
        var byAmount = string.Equals(
            request.SortBy,
            TransactionQueryRequest.SortByAmount,
            StringComparison.OrdinalIgnoreCase);

        var ascending = string.Equals(
            request.SortDirection,
            TransactionQueryRequest.SortAscending,
            StringComparison.OrdinalIgnoreCase);

        // The tiebreaker on Id is not cosmetic. Several rows can share a timestamp
        // to the tick - a bulk import, or one recurring run - and SQL Server is
        // free to order those ties differently between two executions, which lets
        // the same row appear on page 1 and page 2 while another is never seen at
        // all. Id is sequential, so it is a cheap secondary key as well.
        return (byAmount, ascending) switch
        {
            (true, true) => query.OrderBy(x => x.Amount).ThenBy(x => x.Id),
            (true, false) => query.OrderByDescending(x => x.Amount).ThenByDescending(x => x.Id),
            (false, true) => query.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id),
            (false, false) => query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id),
        };
    }

    /// <summary>
    /// Wraps the search term in wildcards after neutralising any the user typed,
    /// so a search for "50%" matches a literal "50%" rather than everything that
    /// starts with 50.
    /// </summary>
    private static string ToLikePattern(string search)
    {
        // The escape character has to be doubled first: escaping the wildcards
        // first would then double their fresh escape prefixes and match nothing.
        // A closing bracket needs no escaping - defusing "[" disarms the range.
        var escaped = search.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

        return $"%{escaped}%";
    }

    /// <summary>
    /// Confirms the category is the caller's own and usable on this side of the
    /// ledger, then settles which currency the row is denominated in.
    /// </summary>
    private async Task<string> ValidateCategoryAndResolveCurrencyAsync(
        Guid userId,
        ITransactionWriteRequest request,
        CancellationToken cancellationToken)
    {
        // Cast to nullable so "no such category" stays distinguishable from a real
        // enum value instead of colliding with default(TransactionType). Another
        // user's category has to read as missing, never as forbidden.
        var categoryType = await db.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Id == request.CategoryId)
            .Select(c => (TransactionType?)c.Type)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Category", request.CategoryId);

        // A category belongs to one side of the ledger. Filing an expense under
        // "Salary" would survive the write and then corrupt every income/expense
        // split, budget and breakdown that reads it back.
        if (categoryType != request.Type)
        {
            var side = categoryType == TransactionType.Income ? "income" : "expenses";

            throw new BusinessRuleException(
                $"That category can only be used for {side}.",
                "category_type_mismatch");
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            return request.CurrencyCode.Trim().ToUpperInvariant();
        }

        var accountCurrency = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.CurrencyCode)
            .FirstOrDefaultAsync(cancellationToken);

        // A hardcoded fallback here would stamp the wrong currency onto real money
        // for every user not on it, and the row keeps that code forever because it
        // is history rather than a preference.
        if (string.IsNullOrWhiteSpace(accountCurrency))
        {
            throw new BusinessRuleException(
                "We could not work out which currency to use. Choose your currency in settings and try again.",
                "currency_unresolved");
        }

        return accountCurrency;
    }

    private static void Apply(Transaction entity, ITransactionWriteRequest request, string currencyCode)
    {
        entity.Type = request.Type;

        // Rounded on the way in so the stored row and the response agree. Left
        // alone, a third decimal is rounded by the provider on its way into
        // decimal(18,2) and the client's optimistic total drifts from the
        // server's with nothing to show why.
        entity.Amount = Money.Round(request.Amount);

        entity.CategoryId = request.CategoryId;
        entity.CurrencyCode = currencyCode;
        entity.PaymentMethod = request.PaymentMethod;

        // Normalised to UTC because the contract promises every timestamp comes
        // back as UTC, and datetimeoffset round-trips whatever offset was written
        // - an unnormalised value would leak the device's zone into the response.
        entity.TransactionDate = request.TransactionDate.ToUniversalTime();

        entity.Description = Normalise(request.Description);
        entity.Merchant = Normalise(request.Merchant);
        entity.Notes = Normalise(request.Notes);
    }

    /// <summary>
    /// Collapses whitespace-only input to null so the contract's "string or null"
    /// stays truthful and a blank note is not stored as something the client will
    /// render as an empty line.
    /// </summary>
    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
