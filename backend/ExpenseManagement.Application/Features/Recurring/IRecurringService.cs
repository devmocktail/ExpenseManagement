namespace ExpenseManagement.Application.Features.Recurring;

/// <summary>
/// Recurring schedules: the caller's own CRUD, plus the generation pass the
/// background worker drives.
///
/// Every CRUD member resolves the user from the authenticated principal and
/// never from an argument, so no route or body value can widen what a request
/// reaches. A schedule that does not exist and one that belongs to another
/// account both raise <c>NotFoundException</c>: answering 403 for the second
/// would confirm the id is real and turn the API into an enumeration oracle.
/// </summary>
public interface IRecurringService
{
    /// <summary>Active schedules first, then paused ones, each in due order.</summary>
    Task<IReadOnlyList<RecurringDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<RecurringDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a schedule and computes its first run: the start date if that is
    /// still ahead, otherwise the first occurrence at or after now — a schedule
    /// backdated to last year must not open with a year of arrears queued.
    /// </summary>
    Task<RecurringDto> CreateAsync(CreateRecurringRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the schedule. The next run is recomputed only when the
    /// recurrence itself moves (start date, frequency or interval), so editing
    /// an amount or a note leaves the run date the user can see untouched.
    /// </summary>
    Task<RecurringDto> UpdateAsync(Guid id, UpdateRecurringRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the schedule; the transactions it already generated stay.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses or resumes the schedule. Resuming rolls the next run forward past
    /// anything missed while it was paused — a subscription nobody was charged
    /// for during the pause must not be charged for retroactively.
    /// </summary>
    Task<RecurringDto> SetPausedAsync(Guid id, bool isPaused, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates the transactions every due schedule owes, across all accounts,
    /// and returns how many rows were created. Called by the background worker,
    /// never from a request: there is no authenticated user in scope, and the
    /// account each row belongs to comes from the schedule itself.
    ///
    /// Safe to call repeatedly and safe to interrupt. Each occurrence carries a
    /// deterministic client reference, so a pass that dies after the insert but
    /// before the schedule advances re-runs into an existing row instead of a
    /// duplicate charge.
    /// </summary>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken);
}
