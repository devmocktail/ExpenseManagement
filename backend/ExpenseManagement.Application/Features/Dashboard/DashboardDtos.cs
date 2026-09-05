using ExpenseManagement.Application.Features.Budgets;
using ExpenseManagement.Application.Features.Transactions;

namespace ExpenseManagement.Application.Features.Dashboard;

/// <summary>
/// Everything the home screen renders, in one payload.
///
/// The shape mirrors <c>DashboardResponse</c> in mobile/src/types/api.ts exactly;
/// System.Text.Json is configured for camelCase, so these PascalCase names land
/// on the wire as the client's contract expects.
/// </summary>
public sealed record DashboardResponseDto
{
    public required string CurrencyCode { get; init; }

    /// <summary>Inclusive UTC start of the period the figures below cover.</summary>
    public required DateTimeOffset PeriodStart { get; init; }

    /// <summary>Exclusive UTC end — half-open, so consecutive periods tile without overlap.</summary>
    public required DateTimeOffset PeriodEnd { get; init; }

    /// <summary>Already formatted in the user's locale, e.g. "September 2026".</summary>
    public required string PeriodLabel { get; init; }

    public required decimal Balance { get; init; }
    public required decimal Income { get; init; }
    public required decimal Expenses { get; init; }

    /// <summary>
    /// Null when the user has no overall budget for this period. The client
    /// branches on null to show a "create a budget" call to action, so a zeroed
    /// summary would be actively wrong here, not merely empty.
    /// </summary>
    public BudgetSummaryDto? Budget { get; init; }

    public required IReadOnlyList<TransactionDto> RecentTransactions { get; init; }
    public required IReadOnlyList<CategoryBreakdownItemDto> CategoryBreakdown { get; init; }
    public required IReadOnlyList<TrendPointDto> WeeklyTrend { get; init; }
}

/// <summary>One slice of the spending donut.</summary>
public sealed record CategoryBreakdownItemDto
{
    /// <summary>
    /// <see cref="Guid.Empty"/> identifies the folded "Other" slice: the wire
    /// contract types this as a non-null string, and no real category id can be
    /// all zeroes, so the client can key its list on it safely.
    /// </summary>
    public required Guid CategoryId { get; init; }

    public required string CategoryName { get; init; }
    public required string CategoryIcon { get; init; }
    public required string CategoryColor { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>Share of the period's total expenses, 0-100.</summary>
    public required decimal Percentage { get; init; }

    public required int TransactionCount { get; init; }
}

/// <summary>One bucket of a trend chart — a single day for the dashboard.</summary>
public sealed record TrendPointDto
{
    /// <summary>UTC instant the bucket opens; for a daily bucket this is local midnight.</summary>
    public required DateTimeOffset Date { get; init; }

    /// <summary>Short axis label, pre-formatted in the user's locale (e.g. "Mon").</summary>
    public required string Label { get; init; }

    public required decimal Income { get; init; }
    public required decimal Expense { get; init; }
}
