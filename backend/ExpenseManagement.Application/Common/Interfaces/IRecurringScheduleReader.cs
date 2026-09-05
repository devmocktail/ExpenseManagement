namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// The one read in this system that deliberately crosses account boundaries:
/// the scheduler's "which schedules are due right now?" scan.
///
/// It gets its own interface because suspending the ownership filter needs that
/// filter's *name*, and the names are a persistence detail (<c>FilterNames</c>)
/// the application layer cannot reference. Confining the escape hatch to a
/// single method that returns nothing but ids means no cross-account row ever
/// arrives through it — the caller still loads each schedule and works one
/// account at a time.
/// </summary>
public interface IRecurringScheduleReader
{
    /// <summary>
    /// Ids of live, unpaused schedules whose next run is at or before
    /// <paramref name="asOf"/>, oldest due first, capped at
    /// <paramref name="max"/> so one pass can never pull an unbounded backlog
    /// into memory.
    ///
    /// Implementations suspend the ownership filter only: soft-deleted
    /// schedules must stay excluded, or a cancelled subscription would start
    /// billing again from a background job nobody is watching.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDueScheduleIdsAsync(
        DateTimeOffset asOf,
        int max,
        CancellationToken cancellationToken);
}
