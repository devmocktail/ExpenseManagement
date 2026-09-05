using System.Globalization;
using ExpenseManagement.Application.Common.Extensions;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Application.Features.Budgets;
using ExpenseManagement.Application.Features.Transactions;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Dashboard;

/// <inheritdoc />
public sealed class DashboardService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IBudgetService budgets) : IDashboardService
{
    private const int RecentTransactionCount = 5;

    /// <summary>Slices beyond this are folded into "Other"; more than seven arcs is an unreadable donut.</summary>
    private const int TopCategoryCount = 6;

    /// <summary>
    /// Buckets in the trend chart. Raising this also means extending the bucket
    /// key in <see cref="GetWeeklyTrendAsync"/>, which names each day boundary
    /// explicitly so the whole grouping can stay in SQL.
    /// </summary>
    private const int TrendDays = 7;

    /// <summary>Neutral grey: "Other" is a leftover, not a category with an identity.</summary>
    private const string OtherCategoryColor = "#94A3B8";

    /// <summary>The domain's default icon key, so the client's icon map is guaranteed to resolve it.</summary>
    private const string OtherCategoryIcon = "category";

    private static readonly DateTimeOffset MinAnchor = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MaxAnchor = new(2200, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task<DashboardResponseDto> GetAsync(DateTimeOffset? at, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var anchor = ValidateAnchor(at ?? clock.UtcNow);

        var settings = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.CurrencyCode, s.Locale, s.TimeZoneId, s.MonthStartDay })
            .FirstOrDefaultAsync(cancellationToken)
            // Settings are written with the account, so their absence is a broken
            // invariant rather than a state to paper over. Guessing a zone instead
            // would file the boundary days of every month under the wrong period
            // and quietly disagree with the user's bank statement.
            ?? throw new NotFoundException("User settings", userId);

        var tz = PeriodCalculator.ResolveTimeZone(settings.TimeZoneId);
        var culture = ResolveCulture(settings.Locale);
        var window = PeriodCalculator.MonthOf(anchor, tz, settings.MonthStartDay);

        // Sequential on purpose. These reads are independent and would love to run
        // concurrently, but they share one scoped EF DbContext - as does the call
        // into IBudgetService - and a DbContext is not thread-safe. Task.WhenAll
        // would need a context per task; without that it buys a few milliseconds in
        // exchange for intermittent "a second operation was started on this context"
        // failures on the most-requested endpoint in the product.
        var (income, expenses) = await GetTotalsAsync(userId, window, cancellationToken);
        var breakdown = await GetCategoryBreakdownAsync(userId, window, expenses, cancellationToken);
        var trend = await GetWeeklyTrendAsync(userId, anchor, tz, culture, cancellationToken);
        var recent = await GetRecentTransactionsAsync(userId, cancellationToken);
        var budget = await budgets.GetOverallSummaryAsync(window, cancellationToken);

        return new DashboardResponseDto
        {
            CurrencyCode = settings.CurrencyCode,
            PeriodStart = window.Start,
            PeriodEnd = window.End,
            PeriodLabel = FormatPeriodLabel(window, tz, culture),
            Balance = Money.Round(income - expenses),
            Income = income,
            Expenses = expenses,
            Budget = budget,
            RecentTransactions = recent,
            CategoryBreakdown = breakdown,
            WeeklyTrend = trend,
        };
    }

    /// <summary>
    /// Both headline totals in one round trip: SQL groups by type and returns at
    /// most two rows, so no amount column is ever streamed into this process.
    /// </summary>
    private async Task<(decimal Income, decimal Expenses)> GetTotalsAsync(
        Guid userId,
        DateRange window,
        CancellationToken cancellationToken)
    {
        var totals = await db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.TransactionDate >= window.Start
                        && t.TransactionDate < window.End)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var income = totals.FirstOrDefault(x => x.Type == TransactionType.Income)?.Total ?? 0m;
        var expenses = totals.FirstOrDefault(x => x.Type == TransactionType.Expense)?.Total ?? 0m;

        return (Money.Round(income), Money.Round(expenses));
    }

    /// <summary>
    /// Expenses only: the donut answers "where did the money go", and folding
    /// income into the same ring would make every slice meaningless.
    /// </summary>
    private async Task<IReadOnlyList<CategoryBreakdownItemDto>> GetCategoryBreakdownAsync(
        Guid userId,
        DateRange window,
        decimal totalExpenses,
        CancellationToken cancellationToken)
    {
        var groups = await db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.Type == TransactionType.Expense
                        && t.TransactionDate >= window.Start
                        && t.TransactionDate < window.End)
            .GroupBy(t => new { t.CategoryId, t.Category.Name, t.Category.Icon, t.Category.Color })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.Name,
                g.Key.Icon,
                g.Key.Color,
                Amount = g.Sum(t => t.Amount),
                TransactionCount = g.Count(),
            })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(cancellationToken);

        var items = new List<CategoryBreakdownItemDto>(TopCategoryCount + 1);

        foreach (var group in groups.Take(TopCategoryCount))
        {
            items.Add(new CategoryBreakdownItemDto
            {
                CategoryId = group.CategoryId,
                CategoryName = group.Name,
                CategoryIcon = group.Icon,
                CategoryColor = group.Color,
                Amount = Money.Round(group.Amount),
                // Measured against the headline expense figure so the slices relate
                // to the number printed above the chart, not to a second total the
                // user cannot see.
                Percentage = Money.Percentage(group.Amount, totalExpenses),
                TransactionCount = group.TransactionCount,
            });
        }

        if (groups.Count > TopCategoryCount)
        {
            // The tail is folded here rather than in SQL because these rows are
            // already aggregates - one per category, a couple of dozen at worst -
            // not the transactions behind them.
            var tail = groups.Skip(TopCategoryCount).ToList();
            var tailAmount = tail.Sum(x => x.Amount);

            items.Add(new CategoryBreakdownItemDto
            {
                CategoryId = Guid.Empty,
                CategoryName = "Other",
                CategoryIcon = OtherCategoryIcon,
                CategoryColor = OtherCategoryColor,
                Amount = Money.Round(tailAmount),
                Percentage = Money.Percentage(tailAmount, totalExpenses),
                TransactionCount = tail.Sum(x => x.TransactionCount),
            });
        }

        return items;
    }

    /// <summary>
    /// Seven daily buckets ending on the anchor's local day, empty days included:
    /// dropping a day with no activity would close the gap in the chart and turn a
    /// quiet stretch into a flat line that reads as steady spending.
    /// </summary>
    private async Task<IReadOnlyList<TrendPointDto>> GetWeeklyTrendAsync(
        Guid userId,
        DateTimeOffset anchor,
        TimeZoneInfo tz,
        CultureInfo culture,
        CancellationToken cancellationToken)
    {
        var days = new DateRange[TrendDays];
        days[TrendDays - 1] = PeriodCalculator.DayOf(anchor, tz);

        for (var i = TrendDays - 2; i >= 0; i--)
        {
            // Step back from midday of the following day rather than subtracting a
            // flat 24 hours: a DST day is 23 or 25 hours long, and midday lands
            // inside the previous calendar day under either.
            days[i] = PeriodCalculator.DayOf(days[i + 1].Start.AddHours(-12), tz);
        }

        var start = days[0].Start;
        var end = days[TrendDays - 1].End;

        // The bucket key counts how many day boundaries an instant has passed. A
        // date function would be shorter, but DATEDIFF-style helpers live in the
        // provider packages this layer deliberately does not reference, and they
        // count UTC midnights - the wrong boundary for anyone not living on UTC.
        // Comparing against the real local boundaries computed above keeps the
        // grouping in SQL and correct in every zone.
        var b1 = days[1].Start;
        var b2 = days[2].Start;
        var b3 = days[3].Start;
        var b4 = days[4].Start;
        var b5 = days[5].Start;
        var b6 = days[6].Start;

        var buckets = await db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.TransactionDate >= start && t.TransactionDate < end)
            .GroupBy(t => new
            {
                t.Type,
                DayIndex = (t.TransactionDate >= b1 ? 1 : 0)
                           + (t.TransactionDate >= b2 ? 1 : 0)
                           + (t.TransactionDate >= b3 ? 1 : 0)
                           + (t.TransactionDate >= b4 ? 1 : 0)
                           + (t.TransactionDate >= b5 ? 1 : 0)
                           + (t.TransactionDate >= b6 ? 1 : 0),
            })
            .Select(g => new { g.Key.Type, g.Key.DayIndex, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        var points = new List<TrendPointDto>(TrendDays);

        for (var i = 0; i < TrendDays; i++)
        {
            var index = i;
            var income = buckets.FirstOrDefault(x => x.DayIndex == index && x.Type == TransactionType.Income)?.Total ?? 0m;
            var expense = buckets.FirstOrDefault(x => x.DayIndex == index && x.Type == TransactionType.Expense)?.Total ?? 0m;

            points.Add(new TrendPointDto
            {
                Date = days[i].Start,
                Label = TimeZoneInfo.ConvertTime(days[i].Start, tz).ToString("ddd", culture),
                Income = Money.Round(income),
                Expense = Money.Round(expense),
            });
        }

        return points;
    }

    /// <summary>
    /// The latest five entries overall, deliberately not clipped to the period: on
    /// the first day of a month the recent-activity card would otherwise render
    /// empty for a user who simply has not spent anything yet today.
    /// </summary>
    private async Task<IReadOnlyList<TransactionDto>> GetRecentTransactionsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            // Backdated entries routinely share a transaction date, so further keys
            // keep the five stable between two refreshes of the same screen.
            .ThenByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Take(RecentTransactionCount)
            .Select(TransactionMappings.ToDto)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Named after the month the window opens in, so a user paid on the 15th sees
    /// "September 2026" for their 15 Sep - 15 Oct period rather than a date range
    /// they have to decode.
    /// </summary>
    private static string FormatPeriodLabel(DateRange window, TimeZoneInfo tz, CultureInfo culture) =>
        TimeZoneInfo.ConvertTime(window.Start, tz).ToString("MMMM yyyy", culture);

    /// <summary>
    /// An anchor near <see cref="DateTimeOffset.MaxValue"/> overflows the month
    /// arithmetic in <see cref="PeriodCalculator"/>, which would surface as a 500
    /// for a value the client fully controls. Rejected as a request error instead.
    /// </summary>
    private static DateTimeOffset ValidateAnchor(DateTimeOffset at) =>
        at >= MinAnchor && at < MaxAnchor
            ? at
            : throw new BusinessRuleException(
                "That date is outside the range this dashboard can show.",
                "invalid_period");

    private static CultureInfo ResolveCulture(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return CultureInfo.InvariantCulture;

        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            // A stale or mistyped locale tag must degrade to neutral labels, not
            // take the home screen down.
            return CultureInfo.InvariantCulture;
        }
    }
}
