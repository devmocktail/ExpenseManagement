using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Accounts;

/// <inheritdoc cref="IAccountService"/>
public sealed class AccountService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IAccountService
{
    /// <summary>
    /// The filtered unique indexes behind the two rules the database enforces.
    /// Matching their names in a provider's exception message is what lets this
    /// layer turn a lost race into a 409 without referencing a database driver.
    /// </summary>
    private const string UniqueNameIndex = "UX_Accounts_UserId_Name";
    private const string UniqueNameLowerIndex = "UX_Accounts_UserId_NameLower";
    private const string UniqueDefaultIndex = "UX_Accounts_UserId_Default";

    public async Task<IReadOnlyList<AccountDto>> ListAsync(
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var query = db.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId);

        // Composed conditionally rather than as `includeArchived || !IsArchived`:
        // the latter compiles to an OR on a parameter, which stops the planner
        // using IX_Accounts_UserId_SortOrder.
        if (!includeArchived)
        {
            query = query.Where(account => !account.IsArchived);
        }

        return await query
            .OrderBy(account => account.SortOrder)
            .ThenBy(account => account.Name)
            .Select(AccountMappings.ToDto(userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        return await ProjectAsync(id, userId, cancellationToken);
    }

    public async Task<AccountDto> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var name = request.Name.Trim();
        var icon = (request.Icon ?? "wallet-outline").Trim().ToLowerInvariant();
        var color = (request.Color ?? "#4F46E5").Trim().ToUpperInvariant();
        var currency = (request.CurrencyCode ?? await DefaultCurrencyAsync(userId, cancellationToken))
            .Trim()
            .ToUpperInvariant();

        await GuardDuplicateNameAsync(userId, name, excludingId: null, cancellationToken);

        // The first account a user creates becomes the default whether or not
        // they asked: a picker with nothing pre-selected is a worse default
        // than a guess, and there is nothing to displace.
        var isFirst = !await db.Accounts.AnyAsync(a => a.UserId == userId, cancellationToken);
        var makeDefault = request.IsDefault == true || isFirst;

        var account = new Account
        {
            UserId = userId,
            Name = name,
            Type = request.Type,
            CurrencyCode = currency,
            OpeningBalance = request.OpeningBalance ?? 0m,
            Institution = request.Institution?.Trim(),
            Last4 = request.Last4?.Trim(),
            Icon = icon,
            Color = color,
            IsArchived = false,
            IsDefault = makeDefault,
            SortOrder = request.SortOrder ?? await NextSortOrderAsync(userId, cancellationToken),
        };

        db.Accounts.Add(account);

        // Only one account per user may carry IsDefault, enforced by a partial
        // unique index. Clearing the incumbent and inserting the replacement
        // have to land together or the index rejects the pair, so they share a
        // transaction rather than relying on statement ordering.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        if (makeDefault)
        {
            await ClearExistingDefaultAsync(userId, exceptId: null, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateName(ex))
        {
            // The check above only narrows the common case. Two taps on a flaky
            // connection can both pass it, and the index is what actually
            // decides; a lost race is the user's own duplicate, not a 500.
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException(DuplicateNameMessage(name));
        }


        // An account created a moment ago cannot be referenced yet, so its
        // counts are zero and its balance is exactly its opening balance.
        // Re-reading to rediscover that would be pure ceremony.
        return new AccountDto(
            account.Id,
            account.Name,
            account.Type,
            account.CurrencyCode,
            account.OpeningBalance,
            Balance: account.OpeningBalance,
            account.Institution,
            account.Last4,
            account.Icon,
            account.Color,
            account.IsDefault,
            account.IsArchived,
            account.SortOrder,
            TransactionCount: 0,
            TransferCount: 0);
    }

    public async Task<AccountDto> UpdateAsync(
        Guid id,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Tracked on purpose: this is a write. The explicit ownership predicate
        // sits on top of the DbContext's global filter so the intent is legible
        // here, and a missing row and a stranger's row give the same 404 — a 403
        // would confirm the id exists.
        var account = await db.Accounts
            .Where(a => a.Id == id && a.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        var name = request.Name.Trim();

        if (!string.Equals(name, account.Name, StringComparison.OrdinalIgnoreCase))
        {
            await GuardDuplicateNameAsync(userId, name, excludingId: id, cancellationToken);
        }

        account.Name = name;
        account.Type = request.Type;
        account.Institution = request.Institution?.Trim();
        account.Last4 = request.Last4?.Trim();

        if (request.OpeningBalance is { } opening) account.OpeningBalance = opening;
        if (request.Icon is { } icon) account.Icon = icon.Trim().ToLowerInvariant();
        if (request.Color is { } color) account.Color = color.Trim().ToUpperInvariant();
        if (request.SortOrder is { } sortOrder) account.SortOrder = sortOrder;

        if (request.IsArchived is { } archived)
        {
            // Archiving the default would leave the picker with nothing
            // pre-selected. Refuse rather than silently promote another account,
            // which would move the user's next expense somewhere they did not choose.
            if (archived && account.IsDefault)
            {
                throw new BusinessRuleException(
                    "This is your default account. Make another account the default before archiving it.",
                    "default_account_archive");
            }

            account.IsArchived = archived;
        }

        account.UpdatedAt = clock.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateName(ex))
        {
            throw new ConflictException(DuplicateNameMessage(name));
        }


        return await ProjectAsync(id, userId, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var account = await db.Accounts
            .Where(a => a.Id == id && a.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        var transactions = await db.Transactions
            .CountAsync(t => t.AccountId == id && t.UserId == userId, cancellationToken);

        var transfers = await db.Transfers
            .CountAsync(
                t => (t.FromAccountId == id || t.ToAccountId == id) && t.UserId == userId,
                cancellationToken);

        // Deleting an account with history would either orphan that history or
        // take it with it, and both rewrite what the user actually spent.
        // Archiving is the operation they want: hidden from pickers, still counted.
        if (transactions > 0 || transfers > 0)
        {
            throw new BusinessRuleException(InUseMessage(account.Name, transactions, transfers), "account_in_use");
        }

        if (account.IsDefault)
        {
            throw new BusinessRuleException(
                "This is your default account. Make another account the default before deleting it.",
                "default_account_delete");
        }

        account.IsDeleted = true;
        account.DeletedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AccountDto> SetDefaultAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var account = await db.Accounts
            .Where(a => a.Id == id && a.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Account), id);

        if (account.IsArchived)
        {
            throw new BusinessRuleException(
                "An archived account cannot be the default. Unarchive it first.",
                "archived_account_default");
        }

        if (account.IsDefault)
        {
            return await ProjectAsync(id, userId, cancellationToken);
        }

        // Clearing the incumbent and setting the replacement must land
        // together: the partial unique index rejects the moment both are true,
        // and leaves the user with no default at all if only the clear commits.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        await ClearExistingDefaultAsync(userId, exceptId: id, cancellationToken);

        account.IsDefault = true;
        account.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);


        return await ProjectAsync(id, userId, cancellationToken);
    }

    // ---------------------------------------------------------------------

    private async Task<AccountDto> ProjectAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        await db.Accounts
            .AsNoTracking()
            .Where(account => account.Id == id && account.UserId == userId)
            .Select(AccountMappings.ToDto(userId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Account), id);

    /// <summary>
    /// Compared with lower() on both sides, which PostgreSQL does not do for
    /// free. Without it "Wallet" and "wallet" would both be accepted as
    /// separate accounts — the kind of duplicate that is invisible in a list
    /// and splits a balance in two.
    /// </summary>
    private async Task GuardDuplicateNameAsync(
        Guid userId,
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var comparable = name.ToLowerInvariant();

        var duplicate = await db.Accounts
            .AsNoTracking()
            .AnyAsync(
                account => account.UserId == userId
                    && account.Name.ToLower() == comparable
                    && (excludingId == null || account.Id != excludingId),
                cancellationToken);

        if (duplicate)
        {
            throw new ConflictException(DuplicateNameMessage(name));
        }
    }

    /// <summary>
    /// Clears the current default via ExecuteUpdate rather than loading it.
    /// There is at most one row and nothing here needs its contents, so a
    /// round trip to fetch it only to flip a boolean would be wasted.
    /// </summary>
    private async Task ClearExistingDefaultAsync(
        Guid userId,
        Guid? exceptId,
        CancellationToken cancellationToken) =>
        await db.Accounts
            .Where(a => a.UserId == userId && a.IsDefault && (exceptId == null || a.Id != exceptId))
            .ExecuteUpdateAsync(
                set => set.SetProperty(a => a.IsDefault, false)
                          .SetProperty(a => a.UpdatedAt, clock.UtcNow),
                cancellationToken);

    private async Task<int> NextSortOrderAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Accounts
            .Where(a => a.UserId == userId)
            .Select(a => (int?)a.SortOrder)
            .MaxAsync(cancellationToken) is { } max
            ? max + 1
            : 0;

    private async Task<string> DefaultCurrencyAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.CurrencyCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "INR";

    /// <summary>
    /// Both name indexes can raise the conflict: the plain one on an exact
    /// match, the lower() one on a case variant. Either is the same thing to
    /// the user.
    /// </summary>
    private static bool IsDuplicateName(DbUpdateException exception) =>
        exception.InnerException?.Message is { } message
        && (message.Contains(UniqueNameIndex, StringComparison.Ordinal)
            || message.Contains(UniqueNameLowerIndex, StringComparison.Ordinal));

    private static string DuplicateNameMessage(string name) =>
        $"You already have an account called \"{name}\".";

    private static string InUseMessage(string name, int transactions, int transfers)
    {
        var parts = new List<string>(2);
        if (transactions > 0) parts.Add($"{transactions} transaction{(transactions == 1 ? "" : "s")}");
        if (transfers > 0) parts.Add($"{transfers} transfer{(transfers == 1 ? "" : "s")}");

        return $"\"{name}\" still has {string.Join(" and ", parts)}. "
             + "Archive it instead — it will disappear from the pickers but keep its history.";
    }
}
