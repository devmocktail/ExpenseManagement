using System.Globalization;
using System.Linq.Expressions;
using ExpenseManagement.Application.Common.Extensions;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Features.Analytics;

/// <summary>
/// Analytics, computed entirely by SQL.
///
/// Every query here is a GROUP BY that returns at most a few dozen rows: two for
/// a summary, one per day or month for a chart, one per category for a
/// breakdown. No method materialises a transaction. That is a hard requirement
/// rather than an optimisation — a user with three years of history has tens of
/// thousands of rows, and a phone on a train cannot be asked to download them in
/// order to draw a pie chart.
/// </summary>
public sealed class AnalyticsService(
    IAppDbContext context,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IAnalyticsService
{
    /// <summary>
    /// A previous-period figure below this is too small to compare honestly:
    /// 90 spent against 9 is "up 900%", which is noise wearing the costume of a
    /// finding. Expressed in the account's own currency and deliberately
    /// generous for INR, the product default.
    /// </summary>
    private const decimal InsightComparisonFloor = 500m;

    /// <summary>Percentage move in a category worth telling someone about.</summary>
    private const decimal CategoryChangeThreshold = 15m;

    /// <summary>Savings-rate moves are in percentage points, so the bar is lower.</summary>
    private const decimal SavingsRateChangeThreshold = 5m;

    private const int MaxInsights = 5;
    private const int TopCategoryCount = 5;

    public async Task<AnalyticsSummaryDto> GetSummaryAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var (current, previous) = ResolveWindows(period, at ?? clock.UtcNow, profile);

        return await BuildSummaryAsync(userId, period, current, previous, profile, cancellationToken);
    }

    public async Task<IReadOnlyList<TrendPointDto>> GetTrendAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var (current, _) = ResolveWindows(period, at ?? clock.UtcNow, profile);

        return await BuildTrendAsync(userId, period, current, profile, cancellationToken);
    }

    /// <summary>
    /// Deliberately the same series as the trend: both charts plot the same two
    /// numbers per bucket, and a second implementation of "September's income"
    /// would eventually disagree with this one over a rounding rule.
    /// </summary>
    public Task<IReadOnlyList<TrendPointDto>> GetIncomeVsExpenseAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken) => GetTrendAsync(period, at, cancellationToken);

    public async Task<IReadOnlyList<CategoryBreakdownItemDto>> GetCategoryBreakdownAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var (current, _) = ResolveWindows(period, at ?? clock.UtcNow, profile);

        return ToBreakdown(await CategoryTotalsAsync(userId, current, cancellationToken));
    }

    public async Task<IReadOnlyList<SpendingInsightDto>> GetInsightsAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var (current, previous) = ResolveWindows(period, at ?? clock.UtcNow, profile);

        var summary = await BuildSummaryAsync(userId, period, current, previous, profile, cancellationToken);
        var currentTotals = await CategoryTotalsAsync(userId, current, cancellationToken);
        var previousTotals = await CategoryTotalsAsync(userId, previous, cancellationToken);

        return BuildInsights(period, current, summary, currentTotals, previousTotals, profile);
    }

    public async Task<AnalyticsResponseDto> GetFullAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var (current, previous) = ResolveWindows(period, at ?? clock.UtcNow, profile);

        // Sequential, not Task.WhenAll: a DbContext is a single-threaded unit of
        // work and concurrent queries on one instance throw. These are index
        // seeks on IX_Transactions_UserId_TransactionDate, not table scans.
        var summary = await BuildSummaryAsync(userId, period, current, previous, profile, cancellationToken);
        var trend = await BuildTrendAsync(userId, period, current, profile, cancellationToken);
        var currentTotals = await CategoryTotalsAsync(userId, current, cancellationToken);
        var previousTotals = await CategoryTotalsAsync(userId, previous, cancellationToken);

        var breakdown = ToBreakdown(currentTotals);

        return new AnalyticsResponseDto
        {
            Summary = summary,
            Trend = trend,
            CategoryBreakdown = breakdown,
            TopCategories = [.. breakdown.Take(TopCategoryCount)],
            Insights = BuildInsights(period, current, summary, currentTotals, previousTotals, profile),
        };
    }

    // -----------------------------------------------------------------------
    // Summary
    // -----------------------------------------------------------------------

    private async Task<AnalyticsSummaryDto> BuildSummaryAsync(
        Guid userId,
        AnalyticsPeriod period,
        DateRange current,
        DateRange previous,
        UserProfile profile,
        CancellationToken cancellationToken)
    {
        var now = await TotalsAsync(userId, current, cancellationToken);
        var before = await TotalsAsync(userId, previous, cancellationToken);

        var savings = Money.Round(now.Income - now.Expenses);

        return new AnalyticsSummaryDto
        {
            CurrencyCode = profile.CurrencyCode,
            PeriodStart = current.Start,
            PeriodEnd = current.End,
            PeriodLabel = PeriodLabel(period, current, profile),
            TotalIncome = now.Income,
            TotalExpenses = now.Expenses,
            Savings = savings,
            SavingsRate = Money.Percentage(savings, now.Income),
            TransactionCount = now.Count,
            AverageDailySpend = Money.Round(now.Expenses / ElapsedDays(current)),
            PreviousTotalIncome = before.Income,
            PreviousTotalExpenses = before.Expenses,
        };
    }

    /// <summary>
    /// One grouped query per window. It returns at most two rows — one per
    /// transaction type — so picking them apart below is not aggregating in
    /// memory; the addition has already happened in SQL.
    /// </summary>
    private async Task<PeriodTotals> TotalsAsync(Guid userId, DateRange window, CancellationToken cancellationToken)
    {
        var rows = await context.Transactions
            .AsNoTracking()
            .Where(AnalyticsMappings.OwnedWithin(userId, window))
            .GroupBy(t => t.Type)
            .Select(g => new TypeTotalRow(g.Key, g.Sum(t => t.Amount), g.Count()))
            .ToListAsync(cancellationToken);

        var income = rows.FirstOrDefault(r => r.Type == TransactionType.Income)?.Amount ?? 0m;
        var expenses = rows.FirstOrDefault(r => r.Type == TransactionType.Expense)?.Amount ?? 0m;

        return new PeriodTotals(Money.Round(income), Money.Round(expenses), rows.Sum(r => r.Count));
    }

    /// <summary>
    /// Days gone by in the window, not the window's length. Dividing September's
    /// spend by 30 on the 2nd reports a tenth of the real daily rate, and does it
    /// exactly when someone is checking whether they are on track. A window
    /// wholly in the past divides by its full length; the divisor never drops
    /// below 1.
    /// </summary>
    private int ElapsedDays(DateRange window)
    {
        var now = clock.UtcNow;
        var upTo = now < window.End ? now : window.End;
        var elapsed = (int)Math.Ceiling((upTo - window.Start).TotalDays);

        return Math.Clamp(elapsed, 1, Math.Max(window.TotalDays, 1));
    }

    // -----------------------------------------------------------------------
    // Trend
    // -----------------------------------------------------------------------

    private async Task<IReadOnlyList<TrendPointDto>> BuildTrendAsync(
        Guid userId,
        AnalyticsPeriod period,
        DateRange window,
        UserProfile profile,
        CancellationToken cancellationToken)
    {
        var byMonth = period == AnalyticsPeriod.Year;
        var buckets = EnumerateBuckets(window, profile.TimeZone, byMonth);
        if (buckets.Count == 0) return [];

        // TransactionDate is UTC. Shifting it by the zone's offset before taking
        // the date parts is what files 01:00 on 1 October in Kolkata under
        // October rather than under 30 September. The offset is sampled once, at
        // the window start, so the hour either side of a DST change can land in
        // the neighbouring bucket — a chart artefact, and the price of bucketing
        // in SQL instead of dragging every row across the wire.
        var offsetMinutes = (int)profile.TimeZone.GetUtcOffset(window.Start).TotalMinutes;

        var scoped = context.Transactions
            .AsNoTracking()
            .Where(AnalyticsMappings.OwnedWithin(userId, window));

        var rows = byMonth
            ? await scoped
                .GroupBy(t => new
                {
                    t.TransactionDate.AddMinutes(offsetMinutes).Year,
                    t.TransactionDate.AddMinutes(offsetMinutes).Month,
                })
                .Select(g => new BucketRow(
                    g.Key.Year,
                    g.Key.Month,
                    1,
                    g.Sum(t => t.Type == TransactionType.Income ? t.Amount : 0m),
                    g.Sum(t => t.Type == TransactionType.Expense ? t.Amount : 0m)))
                .ToListAsync(cancellationToken)
            : await scoped
                .GroupBy(t => new
                {
                    t.TransactionDate.AddMinutes(offsetMinutes).Year,
                    t.TransactionDate.AddMinutes(offsetMinutes).Month,
                    t.TransactionDate.AddMinutes(offsetMinutes).Day,
                })
                .Select(g => new BucketRow(
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    g.Sum(t => t.Type == TransactionType.Income ? t.Amount : 0m),
                    g.Sum(t => t.Type == TransactionType.Expense ? t.Amount : 0m)))
                .ToListAsync(cancellationToken);

        // GROUP BY only emits buckets that have rows. A chart fed those alone
        // draws Monday next to Thursday and silently rewrites the shape of the
        // week, so every bucket is materialised and the quiet ones stay at zero.
        var slots = new Dictionary<DateOnly, int>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            slots[LocalDateOf(buckets[i].Start, profile.TimeZone)] = i;
        }

        var firstDate = LocalDateOf(buckets[0].Start, profile.TimeZone);
        var income = new decimal[buckets.Count];
        var expense = new decimal[buckets.Count];

        foreach (var row in rows)
        {
            var key = new DateOnly(row.Year, row.Month, row.Day);

            // A DST-shifted row can key one day outside the window. Snapping it
            // to the nearer end keeps the chart's totals equal to the summary's;
            // dropping it would make the two disagree for no visible reason.
            if (!slots.TryGetValue(key, out var slot))
            {
                slot = key < firstDate ? 0 : buckets.Count - 1;
            }

            income[slot] += row.Income;
            expense[slot] += row.Expense;
        }

        var points = new List<TrendPointDto>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            points.Add(new TrendPointDto
            {
                Date = buckets[i].Start,
                Label = BucketLabel(period, TimeZoneInfo.ConvertTime(buckets[i].Start, profile.TimeZone), profile.Culture),
                Income = Money.Round(income[i]),
                Expense = Money.Round(expense[i]),
            });
        }

        return points;
    }

    /// <summary>
    /// The chart's buckets, in the user's zone. Each one starts exactly where the
    /// last ended — half-open ranges tile without gaps — so a day that is 23 or
    /// 25 hours long across a DST change still yields exactly one bucket and the
    /// walk cannot drift off the end of the window.
    /// </summary>
    private static List<DateRange> EnumerateBuckets(DateRange window, TimeZoneInfo tz, bool byMonth)
    {
        var buckets = new List<DateRange>();
        var cursor = window.Start;

        while (cursor < window.End)
        {
            var bucket = byMonth
                ? PeriodCalculator.MonthOf(cursor, tz)
                : PeriodCalculator.DayOf(cursor, tz);

            buckets.Add(bucket);

            // A zone whose rules somehow yield a non-advancing range must not
            // spin this loop forever on a request thread.
            if (bucket.End <= cursor) break;

            cursor = bucket.End;
        }

        return buckets;
    }

    // -----------------------------------------------------------------------
    // Category breakdown
    // -----------------------------------------------------------------------

    /// <summary>
    /// Expense totals per category, summed and sorted by the database. The join
    /// to Categories carries the display fields only; the money never leaves SQL
    /// un-aggregated.
    /// </summary>
    private async Task<List<CategoryTotalRow>> CategoryTotalsAsync(
        Guid userId,
        DateRange window,
        CancellationToken cancellationToken) =>
        await context.Transactions
            .AsNoTracking()
            .Where(AnalyticsMappings.OwnedWithin(userId, window))
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => new { t.CategoryId, t.Category.Name, t.Category.Icon, t.Category.Color })
            // Ordered on the aggregate rather than on the projected row so the
            // sort is an ORDER BY SUM(...) the server does, not one this process
            // does after the fact.
            .OrderByDescending(g => g.Sum(t => t.Amount))
            .Select(g => new CategoryTotalRow(
                g.Key.CategoryId,
                g.Key.Name,
                g.Key.Icon,
                g.Key.Color,
                g.Sum(t => t.Amount),
                g.Count()))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Percentages are worked out here rather than in SQL: the rows arrive
    /// already aggregated, one per category, and a window function to divide by
    /// the grand total would buy a second pass over the data for arithmetic on a
    /// handful of values.
    /// </summary>
    private static List<CategoryBreakdownItemDto> ToBreakdown(List<CategoryTotalRow> totals)
    {
        var grandTotal = totals.Sum(r => r.Amount);

        return
        [
            .. totals.Select(r => new CategoryBreakdownItemDto
            {
                CategoryId = r.CategoryId,
                CategoryName = CategoryName(r.Name),
                CategoryIcon = string.IsNullOrWhiteSpace(r.Icon) ? "category" : r.Icon,
                CategoryColor = string.IsNullOrWhiteSpace(r.Color) ? "#6366F1" : r.Color,
                Amount = Money.Round(r.Amount),
                Percentage = Money.Percentage(r.Amount, grandTotal),
                TransactionCount = r.Count,
            }),
        ];
    }

    /// <summary>
    /// The category's own query filters make its join outer, so a soft-deleted
    /// category leaves the display columns null rather than hiding the spending.
    /// The money still moved and still has to appear somewhere.
    /// </summary>
    private static string CategoryName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Uncategorised" : name;

    // -----------------------------------------------------------------------
    // Insights
    // -----------------------------------------------------------------------

    private static IReadOnlyList<SpendingInsightDto> BuildInsights(
        AnalyticsPeriod period,
        DateRange window,
        AnalyticsSummaryDto summary,
        List<CategoryTotalRow> current,
        List<CategoryTotalRow> previous,
        UserProfile profile)
    {
        var insights = new List<SpendingInsightDto>();
        var scope = InsightScope(period, window);
        var noun = PeriodNoun(period);

        if (summary.TotalExpenses <= 0m || current.Count == 0)
        {
            // The only honest thing to say about an empty period.
            insights.Add(new SpendingInsightDto
            {
                Id = $"{scope}:no-spending",
                Severity = InsightSeverity.Neutral,
                Title = "No spending recorded",
                Detail = $"You have not logged an expense this {noun}. Add one to start seeing where your money goes.",
            });

            return insights;
        }

        if (summary.TotalExpenses > summary.TotalIncome)
        {
            var gap = Money.Round(summary.TotalExpenses - summary.TotalIncome);

            insights.Add(new SpendingInsightDto
            {
                Id = $"{scope}:overspent",
                Severity = InsightSeverity.Warning,
                Title = "You spent more than you earned",
                Detail = $"Your expenses ran {FormatAmount(gap, profile)} ahead of your income this {noun}.",
            });
        }

        // A savings rate only means something against income, so both periods
        // need some — otherwise the comparison is against a division by zero.
        if (summary.TotalIncome > 0m && summary.PreviousTotalIncome > 0m)
        {
            var previousRate = Money.Percentage(
                summary.PreviousTotalIncome - summary.PreviousTotalExpenses,
                summary.PreviousTotalIncome);

            var delta = Money.Round(summary.SavingsRate - previousRate);

            if (Math.Abs(delta) >= SavingsRateChangeThreshold)
            {
                var improved = delta > 0m;

                insights.Add(new SpendingInsightDto
                {
                    Id = $"{scope}:savings-rate",
                    Severity = improved ? InsightSeverity.Positive : InsightSeverity.Warning,
                    Title = improved ? "Your savings rate improved" : "Your savings rate slipped",
                    Detail = $"You kept {FormatPercent(summary.SavingsRate, profile)} of your income this {noun}, "
                        + $"{(improved ? "up" : "down")} from {FormatPercent(previousRate, profile)} last {noun}.",
                    ChangePercentage = delta,
                });
            }
        }

        insights.AddRange(CategoryMovers(current, previous, scope, noun, profile));

        var largest = current.OrderByDescending(r => r.Amount).First();
        var largestName = CategoryName(largest.Name);

        insights.Add(new SpendingInsightDto
        {
            Id = $"{scope}:largest-category:{largest.CategoryId}",
            Severity = InsightSeverity.Neutral,
            Title = $"{largestName} took the biggest share",
            Detail = $"{FormatAmount(largest.Amount, profile)} went on {largestName} this {noun}, "
                + $"{FormatPercent(Money.Percentage(largest.Amount, summary.TotalExpenses), profile)} of everything you spent.",
        });

        return [.. insights.Take(MaxInsights)];
    }

    /// <summary>
    /// Categories that moved materially against themselves last period. The
    /// floor on the previous amount is the important half: without it a category
    /// that went from 20 to 200 shouts "up 900%" and buries the findings that
    /// matter. Only the two largest movers are emitted, because a screen of
    /// twelve percentages is a table, not an insight.
    /// </summary>
    private static IEnumerable<SpendingInsightDto> CategoryMovers(
        List<CategoryTotalRow> current,
        List<CategoryTotalRow> previous,
        string scope,
        string noun,
        UserProfile profile)
    {
        var before = previous.ToDictionary(r => r.CategoryId);

        var movers = current
            .Select(row => before.TryGetValue(row.CategoryId, out var was) && was.Amount >= InsightComparisonFloor
                ? (Row: row, Was: was.Amount, Change: Money.Percentage(row.Amount - was.Amount, was.Amount))
                : (Row: row, Was: 0m, Change: 0m))
            .Where(m => m.Was > 0m && Math.Abs(m.Change) >= CategoryChangeThreshold)
            .OrderByDescending(m => Math.Abs(m.Change))
            .Take(2);

        foreach (var mover in movers)
        {
            var up = mover.Change > 0m;
            var name = CategoryName(mover.Row.Name);

            yield return new SpendingInsightDto
            {
                Id = $"{scope}:category-change:{mover.Row.CategoryId}",
                Severity = up ? InsightSeverity.Warning : InsightSeverity.Positive,
                Title = $"{name} is {(up ? "up" : "down")} {FormatPercent(Math.Abs(mover.Change), profile)}",
                Detail = $"You spent {FormatAmount(mover.Row.Amount, profile)} on {name} this {noun}, "
                    + $"against {FormatAmount(mover.Was, profile)} last {noun}.",
                ChangePercentage = mover.Change,
            };
        }
    }

    /// <summary>
    /// Insight ids are scoped to the period they were derived from. A bare
    /// "groceries-up" would mean that dismissing the insight in September also
    /// suppresses the same genuine finding in October, and every month after.
    /// </summary>
    private static string InsightScope(AnalyticsPeriod period, DateRange window) =>
        $"{PeriodNoun(period)}:{window.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    // -----------------------------------------------------------------------
    // Windows, formatting, and the caller's own settings
    // -----------------------------------------------------------------------

    private static (DateRange Current, DateRange Previous) ResolveWindows(
        AnalyticsPeriod period,
        DateTimeOffset anchor,
        UserProfile profile)
    {
        var current = WindowOf(period, anchor, profile);

        // One second before the window opens is unambiguously inside the one
        // before it. Deriving the previous window that way rather than
        // subtracting a fixed span is what makes February compare against
        // January instead of against "the 31 days before now".
        var previous = WindowOf(period, current.Start.AddSeconds(-1), profile);

        return (current, previous);
    }

    /// <summary>
    /// The month window honours the user's own <c>MonthStartDay</c> so that a
    /// month here is the same month their budgets are measured over.
    /// </summary>
    private static DateRange WindowOf(AnalyticsPeriod period, DateTimeOffset anchor, UserProfile profile) => period switch
    {
        AnalyticsPeriod.Week => PeriodCalculator.WeekOf(anchor, profile.TimeZone),
        AnalyticsPeriod.Month => PeriodCalculator.MonthOf(anchor, profile.TimeZone, profile.MonthStartDay),
        AnalyticsPeriod.Year => PeriodCalculator.YearOf(anchor, profile.TimeZone),
        _ => throw new BusinessRuleException("Choose a period of week, month or year.", "invalid_period"),
    };

    private static string PeriodNoun(AnalyticsPeriod period) => period switch
    {
        AnalyticsPeriod.Week => "week",
        AnalyticsPeriod.Year => "year",
        _ => "month",
    };

    private static string PeriodLabel(AnalyticsPeriod period, DateRange window, UserProfile profile)
    {
        var start = TimeZoneInfo.ConvertTime(window.Start, profile.TimeZone);

        // End is exclusive, so the last date a user would recognise as part of
        // the period is one tick earlier — otherwise every week reads as eight
        // days long.
        var last = TimeZoneInfo.ConvertTime(window.End.AddTicks(-1), profile.TimeZone);

        var span = $"{start.ToString("d MMM", profile.Culture)} - {last.ToString("d MMM yyyy", profile.Culture)}";

        return period switch
        {
            AnalyticsPeriod.Week => span,
            AnalyticsPeriod.Year => start.ToString("yyyy", profile.Culture),
            // "September 2026" is a lie for someone whose month starts on payday;
            // those users get the real span instead.
            _ => start.Day == 1 ? start.ToString("MMMM yyyy", profile.Culture) : span,
        };
    }

    private static string BucketLabel(AnalyticsPeriod period, DateTimeOffset localStart, CultureInfo culture) => period switch
    {
        AnalyticsPeriod.Week => localStart.ToString("ddd", culture),
        AnalyticsPeriod.Year => localStart.ToString("MMM", culture),
        _ => localStart.ToString("d MMM", culture),
    };

    private static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo tz) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, tz).Date);

    /// <summary>
    /// The code, not the culture's symbol: a user may well hold an INR account
    /// while formatting numbers as en-GB, and a pound sign on a rupee total is
    /// worse than no symbol at all.
    /// </summary>
    private static string FormatAmount(decimal value, UserProfile profile) =>
        $"{profile.CurrencyCode} {value.ToString("N2", profile.Culture)}";

    private static string FormatPercent(decimal value, UserProfile profile) =>
        $"{value.ToString("0.#", profile.Culture)}%";

    /// <summary>
    /// Time zone, locale and month start come from the user's own settings. A
    /// missing settings row falls back to defaults rather than throwing — the
    /// row is created with the account, and an analytics screen is the wrong
    /// place to discover that it was not.
    /// </summary>
    private async Task<UserProfile> LoadProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await context.UserSettings
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new SettingsRow(s.TimeZoneId, s.Locale, s.CurrencyCode, s.MonthStartDay))
            .FirstOrDefaultAsync(cancellationToken);

        return settings is null
            ? new UserProfile(TimeZoneInfo.Utc, CultureInfo.InvariantCulture, "INR", 1)
            : new UserProfile(
                PeriodCalculator.ResolveTimeZone(settings.TimeZoneId),
                ResolveCulture(settings.Locale),
                string.IsNullOrWhiteSpace(settings.CurrencyCode) ? "INR" : settings.CurrencyCode,
                settings.MonthStartDay);
    }

    private static CultureInfo ResolveCulture(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return CultureInfo.InvariantCulture;

        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            // A stale locale must cost a nicely formatted label, not the request.
            return CultureInfo.InvariantCulture;
        }
    }

    private sealed record UserProfile(TimeZoneInfo TimeZone, CultureInfo Culture, string CurrencyCode, int MonthStartDay);

    private sealed record SettingsRow(string TimeZoneId, string Locale, string CurrencyCode, int MonthStartDay);

    private sealed record PeriodTotals(decimal Income, decimal Expenses, int Count);

    private sealed record TypeTotalRow(TransactionType Type, decimal Amount, int Count);

    private sealed record BucketRow(int Year, int Month, int Day, decimal Income, decimal Expense);

    private sealed record CategoryTotalRow(
        Guid CategoryId,
        string? Name,
        string? Icon,
        string? Color,
        decimal Amount,
        int Count);
}

/// <summary>Expressions shared by every query in this feature.</summary>
internal static class AnalyticsMappings
{
    /// <summary>
    /// Ownership and window in one place, applied by all four aggregate queries.
    ///
    /// The UserId predicate duplicates the DbContext's global ownership filter,
    /// and that is the point: the filter is the net, this is the intent. It also
    /// supplies the leading column of IX_Transactions_UserId_TransactionDate, so
    /// the range stays a seek. The window is half-open, so adjacent periods tile
    /// without counting a boundary transaction twice.
    /// </summary>
    public static Expression<Func<Transaction, bool>> OwnedWithin(Guid userId, DateRange window) =>
        t => t.UserId == userId
            && t.TransactionDate >= window.Start
            && t.TransactionDate < window.End;
}
