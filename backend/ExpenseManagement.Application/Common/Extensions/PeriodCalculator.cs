using ExpenseManagement.Application.Common.Models;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Common.Extensions;

/// <summary>
/// Turns "this month" into concrete UTC instants, in the user's own time zone.
///
/// Doing this naively in UTC is a real bug, not a nicety: for a user in
/// Asia/Kolkata (UTC+5:30), a transaction at 02:00 on 1 October local time is
/// 20:30 on 30 September UTC. A UTC-based month boundary files it under the
/// wrong month, so their dashboard and their bank statement disagree. Every
/// boundary here is computed in local time and only then converted to UTC.
/// </summary>
public static class PeriodCalculator
{
    /// <summary>Resolves an IANA (or Windows) zone id, falling back to UTC rather than throwing.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // A stale or misspelled zone must not break someone's dashboard.
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// The calendar month containing <paramref name="instant"/>, honouring a
    /// custom <paramref name="monthStartDay"/> for users whose financial month
    /// begins on payday rather than the 1st.
    /// </summary>
    public static DateRange MonthOf(DateTimeOffset instant, TimeZoneInfo tz, int monthStartDay = 1)
    {
        monthStartDay = Math.Clamp(monthStartDay, 1, 28);

        var local = TimeZoneInfo.ConvertTime(instant, tz);

        // If we are before this month's start day, the period began last month.
        var anchor = local.Day >= monthStartDay
            ? new DateTime(local.Year, local.Month, monthStartDay, 0, 0, 0, DateTimeKind.Unspecified)
            : new DateTime(local.Year, local.Month, monthStartDay, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(-1);

        return FromLocal(anchor, anchor.AddMonths(1), tz);
    }

    /// <summary>The week containing <paramref name="instant"/>, starting on <paramref name="firstDay"/>.</summary>
    public static DateRange WeekOf(DateTimeOffset instant, TimeZoneInfo tz, DayOfWeek firstDay = DayOfWeek.Monday)
    {
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        var date = local.Date;

        var delta = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        var start = date.AddDays(-delta);

        return FromLocal(start, start.AddDays(7), tz);
    }

    public static DateRange QuarterOf(DateTimeOffset instant, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        var firstMonth = ((local.Month - 1) / 3 * 3) + 1;
        var start = new DateTime(local.Year, firstMonth, 1, 0, 0, 0, DateTimeKind.Unspecified);

        return FromLocal(start, start.AddMonths(3), tz);
    }

    public static DateRange YearOf(DateTimeOffset instant, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        var start = new DateTime(local.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        return FromLocal(start, start.AddYears(1), tz);
    }

    public static DateRange DayOf(DateTimeOffset instant, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        var start = local.Date;

        return FromLocal(start, start.AddDays(1), tz);
    }

    /// <summary>The window for a budget of the given cadence that contains <paramref name="instant"/>.</summary>
    public static DateRange ForBudgetPeriod(
        BudgetPeriod period,
        DateTimeOffset instant,
        TimeZoneInfo tz,
        int monthStartDay = 1) => period switch
        {
            BudgetPeriod.Weekly => WeekOf(instant, tz),
            BudgetPeriod.Monthly => MonthOf(instant, tz, monthStartDay),
            BudgetPeriod.Quarterly => QuarterOf(instant, tz),
            BudgetPeriod.Yearly => YearOf(instant, tz),
            _ => MonthOf(instant, tz, monthStartDay),
        };

    /// <summary>
    /// Advances a window by exactly one period. Used to roll a recurring budget
    /// forward without drifting: the next window starts where this one ended.
    /// </summary>
    public static DateRange NextWindow(DateRange current, BudgetPeriod period, TimeZoneInfo tz, int monthStartDay = 1)
    {
        // Nudge one second past the end so we land unambiguously in the next period.
        var probe = current.End.AddSeconds(1);
        return period == BudgetPeriod.Custom
            ? new DateRange(current.End, current.End + current.Duration)
            : ForBudgetPeriod(period, probe, tz, monthStartDay);
    }

    /// <summary>
    /// Converts a local wall-clock boundary pair to UTC instants.
    /// <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/> throws on
    /// a local time that does not exist (the hour skipped by a DST spring-forward),
    /// which is reachable in zones whose transition lands at midnight — so an
    /// invalid boundary is nudged forward rather than allowed to 500.
    /// </summary>
    private static DateRange FromLocal(DateTime localStart, DateTime localEnd, TimeZoneInfo tz) =>
        new(ToUtc(localStart, tz), ToUtc(localEnd, tz));

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo tz)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        while (tz.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, tz), TimeSpan.Zero);
    }
}
