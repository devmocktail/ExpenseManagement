using ExpenseManagement.Application.Common.Models;

namespace ExpenseManagement.Application.Features.Budgets;

/// <summary>
/// Budget reads and writes for the authenticated caller. Every member resolves
/// the user from the token, never from an argument, so no route or body value
/// can widen what a request can see or change.
/// </summary>
public interface IBudgetService
{
    /// <summary>
    /// The caller's active budgets overlapping the period that contains
    /// <paramref name="at"/> (default: now), each with its spend already summed.
    /// </summary>
    Task<IReadOnlyList<BudgetDto>> ListAsync(DateTimeOffset? at, CancellationToken cancellationToken);

    /// <summary>
    /// Throws <see cref="ExpenseManagement.Domain.Exceptions.NotFoundException"/> when the budget is
    /// missing or belongs to someone else — the two cases are indistinguishable on purpose.
    /// </summary>
    Task<BudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<BudgetDto> CreateAsync(CreateBudgetRequest request, CancellationToken cancellationToken);

    Task<BudgetDto> UpdateAsync(Guid id, UpdateBudgetRequest request, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the budget; past periods stay reportable.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The caller's overall (uncategorised) budget overlapping <paramref name="window"/>,
    /// or null when they have none. Consumed by the dashboard so the home screen
    /// stays a single round trip.
    /// </summary>
    Task<BudgetSummaryDto?> GetOverallSummaryAsync(DateRange window, CancellationToken cancellationToken);
}
