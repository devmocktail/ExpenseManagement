using ExpenseManagement.Application.Common.Extensions;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Budgets;

/// <summary>
/// Budget CRUD plus the derived figures the client is not allowed to compute:
/// spend, headroom, percentage and alert status.
/// </summary>
public sealed class BudgetService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IBudgetService
{
    public async Task<IReadOnlyList<BudgetDto>> ListAsync(DateTimeOffset? at, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var settings = await LoadSettingsAsync(userId, cancellationToken);

        var window = PeriodCalculator.MonthOf(at ?? clock.UtcNow, settings.TimeZone, settings.MonthStartDay);

        // Hoisted out of the struct so EF lifts two plain SQL parameters instead of
        // trying to translate member access on a DateRange.
        var windowStart = window.Start;
        var windowEnd = window.End;

        var rows = await db.Budgets
            .AsNoTracking()
            // Overlap, not containment: a weekly cap that only covers part of the month
            // and a yearly cap that swallows it whole are both live right now, and the
            // half-open comparison is what makes adjacent windows tile without a gap.
            .Where(b => b.UserId == userId
                && b.IsActive
                && b.StartDate < windowEnd
                && b.EndDate > windowStart)
            .OrderBy(b => b.CategoryId == null ? 0 : 1)
            .ThenBy(b => b.Name)
            .Select(BudgetMappings.WithSpend(db.Transactions, userId))
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;

        return rows
            .Select(row => BudgetMappings.ToDto(row, settings.WarningThreshold, settings.CriticalThreshold, now))
            .ToList();
    }

    public async Task<BudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var settings = await LoadSettingsAsync(userId, cancellationToken);

        var row = await ProjectAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException(nameof(Budget), id);

        return BudgetMappings.ToDto(row, settings.WarningThreshold, settings.CriticalThreshold, clock.UtcNow);
    }

    public async Task<BudgetDto> CreateAsync(CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var settings = await LoadSettingsAsync(userId, cancellationToken);

        await EnsureCategoryIsUsableAsync(userId, request.CategoryId, cancellationToken);

        var window = ResolveWindow(request.Period, request.StartDate, request.EndDate, settings);
        await EnsureNoOverlapAsync(userId, request.CategoryId, request.Period, window, excludeBudgetId: null, cancellationToken);

        var budget = new Budget
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Amount = Money.Round(request.Amount),

            // Not client-supplied: a cap must be denominated in the account's currency,
            // otherwise "spent" (summed from transactions) and "amount" would be figures
            // in two different units sitting in the same progress bar.
            CurrencyCode = settings.CurrencyCode,

            Period = request.Period,
            StartDate = window.Start,
            EndDate = window.End,

            // A custom window has no next period to roll into, so it can never recur
            // whatever the request asked for.
            IsRecurring = request.Period != BudgetPeriod.Custom && (request.IsRecurring ?? true),

            IsActive = true,
            WarningThreshold = request.WarningThreshold,
            CriticalThreshold = request.CriticalThreshold,
        };

        db.Budgets.Add(budget);
        await db.SaveChangesAsync(cancellationToken);

        return await ReadBackAsync(userId, budget.Id, settings, cancellationToken);
    }

    public async Task<BudgetDto> UpdateAsync(Guid id, UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var settings = await LoadSettingsAsync(userId, cancellationToken);

        // Tracked on purpose — this is a load-for-mutation, not a read.
        var budget = await db.Budgets
            .Where(b => b.Id == id && b.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Budget), id);

        await EnsureCategoryIsUsableAsync(userId, request.CategoryId, cancellationToken);

        var window = ResolveWindow(request.Period, request.StartDate, request.EndDate, settings);
        var isActive = request.IsActive ?? budget.IsActive;

        // An archived budget may sit on top of a live one: only caps the user can still
        // overspend have to be unique for a category and period.
        if (isActive)
        {
            await EnsureNoOverlapAsync(userId, request.CategoryId, request.Period, window, id, cancellationToken);
        }

        var amount = Money.Round(request.Amount);

        // A moved window or a changed cap invalidates the high-water mark. Leaving it
        // set would suppress every alert until spending passed the *old* threshold
        // again, which on a raised cap can mean the user is never warned at all.
        if (budget.StartDate != window.Start || budget.EndDate != window.End || budget.Amount != amount)
        {
            budget.LastNotifiedThreshold = null;
        }

        budget.Name = request.Name.Trim();
        budget.CategoryId = request.CategoryId;
        budget.Amount = amount;
        budget.Period = request.Period;
        budget.StartDate = window.Start;
        budget.EndDate = window.End;
        budget.IsRecurring = request.Period != BudgetPeriod.Custom && (request.IsRecurring ?? budget.IsRecurring);
        budget.IsActive = isActive;
        budget.WarningThreshold = request.WarningThreshold;
        budget.CriticalThreshold = request.CriticalThreshold;

        await db.SaveChangesAsync(cancellationToken);

        return await ReadBackAsync(userId, id, settings, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var budget = await db.Budgets
            .Where(b => b.Id == id && b.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Budget), id);

        // The context turns Remove into a soft delete, so last quarter's plan stays
        // available to reporting instead of vanishing from history.
        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BudgetSummaryDto?> GetOverallSummaryAsync(DateRange window, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var settings = await LoadSettingsAsync(userId, cancellationToken);

        var windowStart = window.Start;
        var windowEnd = window.End;

        var row = await db.Budgets
            .AsNoTracking()
            .Where(b => b.UserId == userId
                && b.IsActive
                && b.CategoryId == null
                && b.StartDate < windowEnd
                && b.EndDate > windowStart)
            // More than one overall cap can overlap a window — a yearly one and this
            // month's, say. The most recently opened, then the narrowest, is the
            // period the user is actually managing, so that is the one the
            // dashboard shows.
            //
            // The Id tiebreaker is what makes this reproducible rather than merely
            // usually-right: with the default month start of the 1st, January's
            // monthly window and the yearly window open on the same instant, so
            // ordering on StartDate alone leaves SQL Server free to return either
            // row between two executions and the dashboard card flips between the
            // two caps on refresh with nothing the user did to explain it.
            .OrderByDescending(b => b.StartDate)
            .ThenBy(b => b.EndDate)
            .ThenBy(b => b.Id)
            .Select(BudgetMappings.WithSpend(db.Transactions, userId))
            .FirstOrDefaultAsync(cancellationToken);

        // Spend is measured over the budget's own window rather than the caller's: a
        // yearly cap must report the share of the year used, or every month's dashboard
        // would claim the whole year is still untouched.
        return row is null
            ? null
            : BudgetMappings.ToSummary(row, settings.WarningThreshold, settings.CriticalThreshold);
    }

    private async Task<BudgetProjection?> ProjectAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        await db.Budgets
            .AsNoTracking()
            .Where(b => b.Id == id && b.UserId == userId)
            .Select(BudgetMappings.WithSpend(db.Transactions, userId))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Re-reads a written budget through the shared projection so the response
    /// carries the category's display fields and the real spend for the window,
    /// instead of an echo of the request that the next GET would contradict.
    /// </summary>
    private async Task<BudgetDto> ReadBackAsync(
        Guid userId,
        Guid id,
        BudgetSettings settings,
        CancellationToken cancellationToken)
    {
        var row = await ProjectAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException(nameof(Budget), id);

        return BudgetMappings.ToDto(row, settings.WarningThreshold, settings.CriticalThreshold, clock.UtcNow);
    }

    private async Task EnsureCategoryIsUsableAsync(Guid userId, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null) return;

        var type = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == categoryId.Value && c.UserId == userId)
            .Select(c => (TransactionType?)c.Type)
            .FirstOrDefaultAsync(cancellationToken);

        // Another user's category has to read as missing, not as forbidden: a 403 here
        // would confirm that the id exists and turn this into an enumeration oracle.
        if (type is null)
        {
            throw new NotFoundException(nameof(Category), categoryId.Value);
        }

        if (type != TransactionType.Expense)
        {
            throw new BusinessRuleException(
                "A budget can only be set on an expense category.",
                "budget_category_not_expense");
        }
    }

    /// <summary>
    /// Turns the request's cadence and anchor date into the concrete half-open
    /// window the budget is stored with.
    /// </summary>
    private static DateRange ResolveWindow(
        BudgetPeriod period,
        DateTimeOffset startDate,
        DateTimeOffset? endDate,
        BudgetSettings settings)
    {
        if (period != BudgetPeriod.Custom)
        {
            // The client sends any instant inside the period it means. Snapping to the
            // calculator's boundaries in the user's own zone is what makes consecutive
            // windows tile exactly, which the unique index and the roll-forward job both
            // rely on — and it honours a month that starts on payday rather than the 1st.
            return PeriodCalculator.ForBudgetPeriod(period, startDate, settings.TimeZone, settings.MonthStartDay);
        }

        // Repeated from the validator because this service is also reachable from
        // background code that never runs FluentValidation.
        if (endDate is null)
        {
            throw new BusinessRuleException("A custom budget needs an end date.", "budget_end_date_required");
        }

        if (endDate.Value <= startDate)
        {
            throw new BusinessRuleException("The end date must be after the start date.", "budget_window_inverted");
        }

        return new DateRange(startDate, endDate.Value);
    }

    private async Task EnsureNoOverlapAsync(
        Guid userId,
        Guid? categoryId,
        BudgetPeriod period,
        DateRange window,
        Guid? excludeBudgetId,
        CancellationToken cancellationToken)
    {
        var windowStart = window.Start;
        var windowEnd = window.End;

        var query = db.Budgets
            .AsNoTracking()
            .Where(b => b.UserId == userId
                && b.IsActive
                && b.Period == period
                && b.StartDate < windowEnd
                && b.EndDate > windowStart);

        // Split into two branches rather than one `b.CategoryId == categoryId`: an
        // overall budget stores NULL, and NULL is not equal to anything in SQL, so the
        // check would silently pass for exactly the case it is most needed in. Being
        // explicit also keeps each form seekable on its own filtered unique index.
        query = categoryId is null
            ? query.Where(b => b.CategoryId == null)
            : query.Where(b => b.CategoryId == categoryId.Value);

        if (excludeBudgetId is { } excluded)
        {
            query = query.Where(b => b.Id != excluded);
        }

        // Two live caps for the same thing make "remaining" ambiguous — the client
        // cannot say which one the next expense eats into.
        if (await query.AnyAsync(cancellationToken))
        {
            throw new ConflictException(categoryId is null
                ? "You already have an overall budget covering that period."
                : "You already have a budget for that category in that period.");
        }
    }

    private async Task<BudgetSettings> LoadSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.TimeZoneId,
                s.MonthStartDay,
                s.BudgetWarningThreshold,
                s.BudgetCriticalThreshold,
                s.CurrencyCode,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // A settings row is created with the account, so this fallback should be
        // unreachable — but a budget screen that renders in UTC beats one that 500s
        // because a row went missing.
        return settings is null
            ? new BudgetSettings(TimeZoneInfo.Utc, MonthStartDay: 1, WarningThreshold: 75, CriticalThreshold: 90, CurrencyCode: "INR")
            : new BudgetSettings(
                PeriodCalculator.ResolveTimeZone(settings.TimeZoneId),
                settings.MonthStartDay,
                settings.BudgetWarningThreshold,
                settings.BudgetCriticalThreshold,
                settings.CurrencyCode);
    }

    /// <summary>The account preferences every budget operation needs, read once per call.</summary>
    private sealed record BudgetSettings(
        TimeZoneInfo TimeZone,
        int MonthStartDay,
        int WarningThreshold,
        int CriticalThreshold,
        string CurrencyCode);
}
