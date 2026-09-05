namespace ExpenseManagement.Application.Features.Analytics;

/// <summary>
/// Read-only aggregation over a user's transactions.
///
/// Every method here resolves its window from the caller's own settings and
/// aggregates in the database. Nothing in this interface returns transactions:
/// the mobile client must never download a ledger in order to add it up, both
/// because it is slow on a phone and because two implementations of "total"
/// inevitably disagree.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Totals for the period containing <paramref name="at"/> (default: now), plus the previous one for comparison.</summary>
    Task<AnalyticsSummaryDto> GetSummaryAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Income and expense per bucket — a day for week and month periods, a month
    /// for year. Buckets with no transactions are returned as zeroes so a chart
    /// draws a flat line rather than closing the gap.
    /// </summary>
    Task<IReadOnlyList<TrendPointDto>> GetTrendAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken);

    /// <summary>Expenses per category, largest first, each with its share of the period total.</summary>
    Task<IReadOnlyList<CategoryBreakdownItemDto>> GetCategoryBreakdownAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken);

    /// <summary>The same buckets as <see cref="GetTrendAsync"/>, for the two-series comparison chart.</summary>
    Task<IReadOnlyList<TrendPointDto>> GetIncomeVsExpenseAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Observations derived strictly from the user's own figures. Returns an
    /// empty list when the data does not support any — filler undermines trust
    /// in the ones that are real.
    /// </summary>
    Task<IReadOnlyList<SpendingInsightDto>> GetInsightsAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken);

    /// <summary>Summary, trend, breakdown, top categories and insights in one response.</summary>
    Task<AnalyticsResponseDto> GetFullAsync(
        AnalyticsPeriod period,
        DateTimeOffset? at,
        CancellationToken cancellationToken);
}
