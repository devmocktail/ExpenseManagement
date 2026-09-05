using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Infrastructure.Services;

/// <summary>
/// One of exactly two sanctioned cross-user queries in this codebase (the other
/// is <see cref="DeviceTokenReader"/>). The scheduler runs with no principal, so
/// the ownership filter would otherwise compare every row's UserId to NULL and
/// find nothing to do.
///
/// It is safe because nothing but primary keys crosses the boundary: no amount,
/// no merchant, no name, nothing that could be shown to the wrong person or
/// leaked through a log. The caller takes the ids and loads each schedule
/// through the ordinary user-scoped path, one account at a time.
/// </summary>
public sealed class RecurringScheduleReader(AppDbContext db) : IRecurringScheduleReader
{
    public async Task<IReadOnlyList<Guid>> GetDueScheduleIdsAsync(
        DateTimeOffset asOf,
        int max,
        CancellationToken cancellationToken)
    {
        // A non-positive cap means "no work this pass". Handing it to Take would
        // ask SQL Server for TOP (0) or throw, and neither is what a caller
        // computing a remaining budget of runs actually meant.
        if (max <= 0) return [];

        return await db.RecurringTransactions
            // Only the ownership filter is suspended. SoftDelete stays on, so a
            // schedule the user cancelled can never start billing again from a
            // background job nobody is watching.
            .IgnoreQueryFilters([FilterNames.UserOwnership])
            .AsNoTracking()
            // The predicate deliberately restates IsPaused rather than relying on
            // IsDueAt: the entity method cannot be translated to SQL, and these
            // three columns are exactly the filter on IX_RecurringTransactions_
            // NextRunDate, so the scan becomes a seek inside the small live set.
            // EndDate varies per row and stays a residual check.
            .Where(schedule =>
                !schedule.IsPaused
                && schedule.NextRunDate <= asOf
                && (schedule.EndDate == null || schedule.NextRunDate <= schedule.EndDate))
            .OrderBy(schedule => schedule.NextRunDate)
            .Take(max)
            .Select(schedule => schedule.Id)
            .ToListAsync(cancellationToken);
    }
}
