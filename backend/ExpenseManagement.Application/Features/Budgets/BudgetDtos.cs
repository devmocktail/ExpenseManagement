using System.Linq.Expressions;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Budgets;

/// <summary>
/// A budget as the client renders it: the stored cap plus every figure derived
/// from this window's spending. The client never sums transactions itself, so
/// two devices can never disagree about how much is left.
/// </summary>
public sealed record BudgetDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Null for the overall budget covering every category.</summary>
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? CategoryIcon { get; init; }
    public string? CategoryColor { get; init; }

    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public required BudgetPeriod Period { get; init; }

    public required DateTimeOffset StartDate { get; init; }

    /// <summary>Exclusive.</summary>
    public required DateTimeOffset EndDate { get; init; }

    public required bool IsRecurring { get; init; }
    public required bool IsActive { get; init; }

    public required decimal Spent { get; init; }

    /// <summary>Goes negative once the cap is passed; "over by 400" tells a user more than a floor at zero.</summary>
    public required decimal Remaining { get; init; }

    public required decimal PercentageUsed { get; init; }

    /// <summary>One of <see cref="BudgetStatuses"/>.</summary>
    public required string Status { get; init; }

    public required int DaysRemaining { get; init; }
}

/// <summary>The compact form the dashboard embeds for the overall budget.</summary>
public sealed record BudgetSummaryDto
{
    public required Guid BudgetId { get; init; }
    public required decimal Amount { get; init; }
    public required decimal Spent { get; init; }
    public required decimal Remaining { get; init; }
    public required decimal Percentage { get; init; }
    public required string Status { get; init; }
}

/// <summary>
/// The four values of the client's BudgetStatus union.
///
/// Deliberately strings and not a C# enum: every other enum in this API crosses
/// the wire as a PascalCase name ("Monthly", "Expense"), so the registered
/// JsonStringEnumConverter would emit "Ok" where the client expects "ok".
/// </summary>
public static class BudgetStatuses
{
    public const string Ok = "ok";
    public const string Warning = "warning";
    public const string Critical = "critical";
    public const string Exceeded = "exceeded";
}

/// <summary>
/// The fields create and update share, so one set of validation rules covers
/// both and the two request shapes cannot drift apart.
/// </summary>
public interface IBudgetWriteRequest
{
    string Name { get; }
    Guid? CategoryId { get; }
    decimal Amount { get; }
    BudgetPeriod Period { get; }
    DateTimeOffset StartDate { get; }
    DateTimeOffset? EndDate { get; }
    bool? IsRecurring { get; }
    int? WarningThreshold { get; }
    int? CriticalThreshold { get; }
}

/// <summary>
/// Request bodies carry no <c>required</c> members on purpose: System.Text.Json
/// rejects a payload with a missing required property before FluentValidation
/// ever runs, turning a friendly field message into an opaque parse failure.
/// </summary>
public sealed record CreateBudgetRequest : IBudgetWriteRequest
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Omit or send null for an overall budget across every category.</summary>
    public Guid? CategoryId { get; init; }

    public decimal Amount { get; init; }

    public BudgetPeriod Period { get; init; } = BudgetPeriod.Monthly;

    /// <summary>Any instant inside the intended window; the server snaps it to the period's boundaries.</summary>
    public DateTimeOffset StartDate { get; init; }

    /// <summary>Required only for <see cref="BudgetPeriod.Custom"/>; derived from the period otherwise.</summary>
    public DateTimeOffset? EndDate { get; init; }

    public bool? IsRecurring { get; init; }

    /// <summary>Null falls back to the account-wide alert setting.</summary>
    public int? WarningThreshold { get; init; }

    public int? CriticalThreshold { get; init; }
}

/// <summary>The create body plus the archive switch.</summary>
public sealed record UpdateBudgetRequest : IBudgetWriteRequest
{
    public string Name { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public decimal Amount { get; init; }
    public BudgetPeriod Period { get; init; } = BudgetPeriod.Monthly;
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public bool? IsRecurring { get; init; }
    public int? WarningThreshold { get; init; }
    public int? CriticalThreshold { get; init; }

    /// <summary>Null leaves the budget's current state alone.</summary>
    public bool? IsActive { get; init; }
}

/// <summary>
/// One row of any budget query: the columns the DTOs need, the category's
/// display fields, this window's spend, and the per-budget threshold overrides,
/// which no DTO exposes but the status calculation depends on.
/// </summary>
internal sealed record BudgetProjection
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string? CategoryIcon { get; init; }
    public string? CategoryColor { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public required BudgetPeriod Period { get; init; }
    public required DateTimeOffset StartDate { get; init; }
    public required DateTimeOffset EndDate { get; init; }
    public required bool IsRecurring { get; init; }
    public required bool IsActive { get; init; }
    public int? WarningThreshold { get; init; }
    public int? CriticalThreshold { get; init; }
    public required decimal Spent { get; init; }
}

internal static class BudgetMappings
{
    /// <summary>
    /// The single projection every budget read shares: list, detail and the
    /// dashboard summary.
    ///
    /// Spend is a correlated aggregate rather than a follow-up round trip. The
    /// database sums each budget's own window inside the statement that reads
    /// the budgets, so a user with twenty caps costs one query instead of
    /// twenty-one and no transaction row is ever shipped here to be added up.
    /// </summary>
    /// <param name="transactions">
    /// The transactions query root. Passed in rather than closed over so this stays a
    /// pure expression factory; its ownership and soft-delete filters still apply
    /// inside the subquery.
    /// </param>
    public static Expression<Func<Budget, BudgetProjection>> WithSpend(
        IQueryable<Transaction> transactions,
        Guid userId) =>
        budget => new BudgetProjection
        {
            Id = budget.Id,
            Name = budget.Name,
            CategoryId = budget.CategoryId,
            CategoryName = budget.Category!.Name,
            CategoryIcon = budget.Category!.Icon,
            CategoryColor = budget.Category!.Color,
            Amount = budget.Amount,
            CurrencyCode = budget.CurrencyCode,
            Period = budget.Period,
            StartDate = budget.StartDate,
            EndDate = budget.EndDate,
            IsRecurring = budget.IsRecurring,
            IsActive = budget.IsActive,
            WarningThreshold = budget.WarningThreshold,
            CriticalThreshold = budget.CriticalThreshold,

            // Income is filtered out, never netted off: a salary landing mid-month must
            // not hand back headroom the user has already spent. An overall budget
            // (null CategoryId) counts every expense in its window, a category budget
            // only its own.
            Spent = transactions
                .Where(t => t.UserId == userId
                    && t.Type == TransactionType.Expense
                    && t.TransactionDate >= budget.StartDate
                    && t.TransactionDate < budget.EndDate
                    && (budget.CategoryId == null || t.CategoryId == budget.CategoryId))
                .Sum(t => (decimal?)t.Amount) ?? 0m,
        };

    public static BudgetDto ToDto(
        BudgetProjection row,
        int defaultWarning,
        int defaultCritical,
        DateTimeOffset now)
    {
        var spent = Money.Round(row.Spent);
        var percentage = Money.Percentage(spent, row.Amount);

        return new BudgetDto
        {
            Id = row.Id,
            Name = row.Name,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            CategoryIcon = row.CategoryIcon,
            CategoryColor = row.CategoryColor,
            Amount = row.Amount,
            CurrencyCode = row.CurrencyCode,
            Period = row.Period,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            IsRecurring = row.IsRecurring,
            IsActive = row.IsActive,
            Spent = spent,
            Remaining = Money.Remaining(row.Amount, spent),
            PercentageUsed = percentage,
            Status = ResolveStatus(percentage, row.WarningThreshold, row.CriticalThreshold, defaultWarning, defaultCritical),
            DaysRemaining = DaysRemaining(row.EndDate, now),
        };
    }

    public static BudgetSummaryDto ToSummary(
        BudgetProjection row,
        int defaultWarning,
        int defaultCritical)
    {
        var spent = Money.Round(row.Spent);
        var percentage = Money.Percentage(spent, row.Amount);

        return new BudgetSummaryDto
        {
            BudgetId = row.Id,
            Amount = row.Amount,
            Spent = spent,
            Remaining = Money.Remaining(row.Amount, spent),
            Percentage = percentage,
            Status = ResolveStatus(percentage, row.WarningThreshold, row.CriticalThreshold, defaultWarning, defaultCritical),
        };
    }

    /// <summary>
    /// Status is derived from the same rounded percentage the client displays, so a
    /// card can never read "100%" beside a "warning" chip. A zero-amount budget gets
    /// 0% out of <see cref="Money.Percentage"/> instead of a division by zero.
    /// </summary>
    private static string ResolveStatus(
        decimal percentage,
        int? warningOverride,
        int? criticalOverride,
        int defaultWarning,
        int defaultCritical)
    {
        var warning = warningOverride ?? defaultWarning;
        var critical = criticalOverride ?? defaultCritical;

        // Critical is tested before warning so an inverted pair, which the database
        // constrains today but an older row may still carry, degrades to the louder
        // state instead of never escalating at all.
        if (percentage >= 100m) return BudgetStatuses.Exceeded;
        if (percentage >= critical) return BudgetStatuses.Critical;
        if (percentage >= warning) return BudgetStatuses.Warning;

        return BudgetStatuses.Ok;
    }

    private static int DaysRemaining(DateTimeOffset end, DateTimeOffset now)
    {
        if (end <= now) return 0;

        // Rounded up rather than truncated: with six hours left the honest answer is
        // one day, and 0 would render as a window that has already closed.
        return (int)Math.Ceiling((end - now).TotalDays);
    }
}
