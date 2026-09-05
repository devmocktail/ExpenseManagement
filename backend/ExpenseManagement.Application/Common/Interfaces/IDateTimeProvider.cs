namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// Indirection over the clock so budget windows, recurrence rolls and token
/// expiry can be tested deterministically instead of with sleeps.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Current instant in UTC. Everything persisted uses this.</summary>
    DateTimeOffset UtcNow { get; }

    DateOnly TodayUtc { get; }
}
