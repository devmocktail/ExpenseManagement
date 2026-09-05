using ExpenseManagement.Application.Common.Interfaces;

namespace ExpenseManagement.Infrastructure.Services;

/// <summary>
/// The real clock. Tests substitute a fixed one so recurrence rolls, budget
/// windows and token expiry are deterministic instead of depending on when the
/// suite happens to run.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
