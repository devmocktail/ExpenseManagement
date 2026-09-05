using ExpenseManagement.Application.Common.Interfaces;
using FluentValidation;

namespace ExpenseManagement.Application.Features.Analytics;

/// <summary>
/// The windows the analytics screens can ask for.
///
/// The wire values are lower-case (<c>week</c>, <c>month</c>, <c>year</c>) while
/// the members are PascalCase; that is safe because this enum only ever arrives
/// as a query-string value and MVC's enum binding is case-insensitive. It is
/// never serialised into a response, so no converter is needed for it.
/// </summary>
public enum AnalyticsPeriod : byte
{
    Week = 1,
    Month = 2,
    Year = 3,
}

/// <summary>Bound from the query string of every analytics endpoint.</summary>
public sealed record AnalyticsQuery
{
    public AnalyticsPeriod Period { get; init; } = AnalyticsPeriod.Month;

    /// <summary>
    /// Anchor instant. The server resolves the window *containing* it in the
    /// user's own zone, so the client never has to compute a boundary itself
    /// and can never disagree with the server about where a month begins.
    /// </summary>
    public DateTimeOffset? At { get; init; }
}

public sealed class AnalyticsQueryValidator : AbstractValidator<AnalyticsQuery>
{
    /// <summary>Nothing in this product predates it; an anchor below this is a client bug or a probe.</summary>
    private static readonly DateTimeOffset EarliestAnchor = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public AnalyticsQueryValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Period)
            .IsInEnum()
            .WithMessage("Choose a period of week, month or year.");

        RuleFor(x => x.At)
            .Must(at => at is null || at.Value >= EarliestAnchor)
            .WithMessage("Analytics are only available for dates from the year 2000 onwards.");

        RuleFor(x => x.At)
            // A day of slack, not zero: a phone in UTC+14 is legitimately a
            // calendar day ahead of the server, and its "today" must not 400.
            .Must(at => at is null || at.Value <= clock.UtcNow.AddDays(1))
            .WithMessage("You cannot view analytics for a future date.");
    }
}

/// <summary>
/// Severity of an insight. Lower-case on the wire, unlike the PascalCase domain
/// enums, so it travels as a string rather than as an enum needing its own
/// naming policy.
/// </summary>
public static class InsightSeverity
{
    public const string Positive = "positive";
    public const string Neutral = "neutral";
    public const string Warning = "warning";
}

public sealed record AnalyticsSummaryDto
{
    public required string CurrencyCode { get; init; }
    public required DateTimeOffset PeriodStart { get; init; }

    /// <summary>Exclusive, matching the half-open windows used throughout the system.</summary>
    public required DateTimeOffset PeriodEnd { get; init; }

    /// <summary>Pre-formatted in the user's locale so every client renders it identically.</summary>
    public required string PeriodLabel { get; init; }

    public required decimal TotalIncome { get; init; }
    public required decimal TotalExpenses { get; init; }
    public required decimal Savings { get; init; }

    /// <summary>Savings as a percentage of income; 0 when there was no income.</summary>
    public required decimal SavingsRate { get; init; }

    public required int TransactionCount { get; init; }

    /// <summary>Expenses divided by the days *elapsed* so far, never the length of the window.</summary>
    public required decimal AverageDailySpend { get; init; }

    public required decimal PreviousTotalIncome { get; init; }
    public required decimal PreviousTotalExpenses { get; init; }
}

/// <summary>One bucket of a chart: a day for week/month periods, a month for year.</summary>
public sealed record TrendPointDto
{
    /// <summary>Bucket start in UTC; the client renders it in the user's zone.</summary>
    public required DateTimeOffset Date { get; init; }

    public required string Label { get; init; }
    public required decimal Income { get; init; }
    public required decimal Expense { get; init; }
}

public sealed record CategoryBreakdownItemDto
{
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string CategoryIcon { get; init; }
    public required string CategoryColor { get; init; }
    public required decimal Amount { get; init; }

    /// <summary>Share of the period's total expenses, to two decimal places.</summary>
    public required decimal Percentage { get; init; }

    public required int TransactionCount { get; init; }
}

public sealed record SpendingInsightDto
{
    /// <summary>
    /// Stable for a given finding within a given period, and scoped by that
    /// period: dismissing "Groceries is up 30%" in September must not also
    /// suppress the same finding in October.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>One of <see cref="InsightSeverity"/>.</summary>
    public required string Severity { get; init; }

    public required string Title { get; init; }
    public required string Detail { get; init; }

    /// <summary>Null unless the insight is a comparison against the previous period.</summary>
    public decimal? ChangePercentage { get; init; }
}

/// <summary>Everything the analytics screen needs, in one round trip.</summary>
public sealed record AnalyticsResponseDto
{
    public required AnalyticsSummaryDto Summary { get; init; }
    public required IReadOnlyList<TrendPointDto> Trend { get; init; }
    public required IReadOnlyList<CategoryBreakdownItemDto> CategoryBreakdown { get; init; }

    /// <summary>The head of <see cref="CategoryBreakdown"/>, so the client does not slice it itself.</summary>
    public required IReadOnlyList<CategoryBreakdownItemDto> TopCategories { get; init; }

    public required IReadOnlyList<SpendingInsightDto> Insights { get; init; }
}
