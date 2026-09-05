using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Application.Features.Recurring;

/// <summary>
/// Where a schedule's occurrences fall on a calendar. Pure and side-effect
/// free: no clock, no database, no ambient user — every instant it needs is an
/// argument, so the whole of it can be pinned down in unit tests.
///
/// Two rules drive the implementation.
///
/// Every occurrence is computed from the schedule's ORIGINAL anchor rather than
/// from the previous occurrence. Adding a month repeatedly to the result is the
/// classic bug: a rent that starts on 31 January lands on 28 February, and the
/// next hop from *that* gives 28 March, 28 April... the schedule has silently
/// migrated to the 28th for good. Anchored on day 31 and clamped per target
/// month, February gives 28/29 and March returns to the 31st.
///
/// The arithmetic happens on the user's wall clock, not in UTC. Rent due on the
/// 1st at 09:00 local must stay 09:00 local after a DST transition; a schedule
/// advanced by a fixed 30 days of elapsed time would drift an hour and, twice a
/// year, across a date boundary.
/// </summary>
public static class RecurrenceCalculator
{
    /// <summary>
    /// The next occurrence strictly after <paramref name="current"/>, treating
    /// <paramref name="current"/> itself as the anchor. Use the overload that
    /// takes an explicit anchor whenever the schedule's start date is known —
    /// this one cannot tell a clamped 28 February from a genuine one.
    /// </summary>
    public static DateTimeOffset Next(
        DateTimeOffset current,
        RecurrenceFrequency frequency,
        int interval,
        TimeZoneInfo timeZone) => Next(current, current, frequency, interval, timeZone);

    /// <summary>
    /// The first occurrence of the series anchored at <paramref name="anchor"/>
    /// that falls strictly after <paramref name="current"/>. A
    /// <paramref name="current"/> before the series begins yields the anchor.
    /// </summary>
    public static DateTimeOffset Next(
        DateTimeOffset anchor,
        DateTimeOffset current,
        RecurrenceFrequency frequency,
        int interval,
        TimeZoneInfo timeZone)
    {
        var step = NormaliseInterval(interval);
        var index = EstimateIndex(anchor, current, frequency, step, timeZone);

        // The estimate is at most one period out — month lengths and DST are what
        // it cannot account for — so these walks settle in an iteration or two
        // whether the schedule is one period or ten years overdue.
        while (Occurrence(anchor, index, frequency, step, timeZone) <= current) index++;
        while (index > 0 && Occurrence(anchor, index - 1, frequency, step, timeZone) > current) index--;

        return Occurrence(anchor, index, frequency, step, timeZone);
    }

    /// <summary>
    /// The first occurrence at or after <paramref name="notBefore"/>. This is
    /// what turns a schedule whose start date has already passed into a next run
    /// in the future, without walking every missed occurrence one at a time.
    /// </summary>
    public static DateTimeOffset FirstOnOrAfter(
        DateTimeOffset anchor,
        DateTimeOffset notBefore,
        RecurrenceFrequency frequency,
        int interval,
        TimeZoneInfo timeZone)
    {
        var step = NormaliseInterval(interval);
        var index = EstimateIndex(anchor, notBefore, frequency, step, timeZone);

        while (Occurrence(anchor, index, frequency, step, timeZone) < notBefore) index++;
        while (index > 0 && Occurrence(anchor, index - 1, frequency, step, timeZone) >= notBefore) index--;

        return Occurrence(anchor, index, frequency, step, timeZone);
    }

    /// <summary>
    /// Occurrence number <paramref name="index"/> of the series, counting the
    /// anchor itself as 0.
    /// </summary>
    public static DateTimeOffset Occurrence(
        DateTimeOffset anchor,
        int index,
        RecurrenceFrequency frequency,
        int interval,
        TimeZoneInfo timeZone)
    {
        // Returned verbatim rather than round-tripped through the zone: during the
        // repeated hour of a DST fall-back a wall clock maps to two instants, and
        // the start date the user actually chose must not be nudged by an hour.
        if (index <= 0) return anchor;

        var steps = NormaliseInterval(interval) * (long)index;
        var local = TimeZoneInfo.ConvertTime(anchor, timeZone).DateTime;

        // AddMonths/AddYears clamp to the last valid day of the target month, which
        // is exactly the wanted behaviour *because* the operand is always the
        // original anchor: 31 Jan +1 month is 28 Feb, +2 months is 31 Mar.
        var next = frequency switch
        {
            RecurrenceFrequency.Daily => local.AddDays(steps),
            RecurrenceFrequency.Weekly => local.AddDays(steps * 7),
            RecurrenceFrequency.Monthly => local.AddMonths(checked((int)steps)),
            RecurrenceFrequency.Yearly => local.AddYears(checked((int)steps)),
            _ => local.AddMonths(checked((int)steps)),
        };

        return ToUtc(next, timeZone);
    }

    /// <summary>
    /// How many whole periods separate the anchor from <paramref name="target"/>.
    /// Never exact by design — it is a starting point the callers correct — but
    /// it turns "a decade of missed daily occurrences" into a couple of
    /// comparisons instead of 3,650 of them.
    /// </summary>
    private static int EstimateIndex(
        DateTimeOffset anchor,
        DateTimeOffset target,
        RecurrenceFrequency frequency,
        int step,
        TimeZoneInfo timeZone)
    {
        var from = TimeZoneInfo.ConvertTime(anchor, timeZone).DateTime;
        var to = TimeZoneInfo.ConvertTime(target, timeZone).DateTime;

        var elapsedDays = (to.Date - from.Date).TotalDays;

        var periods = frequency switch
        {
            RecurrenceFrequency.Daily => elapsedDays / step,
            RecurrenceFrequency.Weekly => elapsedDays / (7d * step),
            RecurrenceFrequency.Monthly => (((to.Year - from.Year) * 12) + to.Month - from.Month) / (double)step,
            RecurrenceFrequency.Yearly => (to.Year - from.Year) / (double)step,
            _ => 0d,
        };

        // Clamped at zero: a target before the series starts is not a negative
        // occurrence, it is "the series has not run yet".
        return periods <= 0 ? 0 : (int)Math.Min(periods, int.MaxValue - 2);
    }

    /// <summary>
    /// A zero or negative interval would make every occurrence land on the anchor
    /// and the scheduler re-fire the same date forever. The database rejects such
    /// a row, but this must hold for values that never reach it.
    /// </summary>
    private static int NormaliseInterval(int interval) => interval < 1 ? 1 : interval;

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // A spring-forward deletes an hour of wall clock, and a schedule sitting in
        // it has no valid local instant. Converting would throw, so the occurrence
        // slides forward to the first time that exists — late, never skipped.
        while (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        // An ambiguous time (the repeated hour of a fall-back) resolves to standard
        // time. Arbitrary but deterministic, which is what matters: the same
        // schedule must not compute two different instants on two different runs.
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone), TimeSpan.Zero);
    }
}
