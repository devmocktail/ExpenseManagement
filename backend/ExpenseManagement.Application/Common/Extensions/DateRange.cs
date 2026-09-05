namespace ExpenseManagement.Application.Common.Models;

/// <summary>
/// A half-open UTC interval <c>[Start, End)</c>.
///
/// Half-open is used everywhere in this system on purpose: closed ranges force
/// every query to guess an end-of-day sentinel (23:59:59? .999? .9999999?) and
/// silently drop transactions recorded in the gap. With an exclusive end,
/// "September" is simply <c>[Sep 1 00:00, Oct 1 00:00)</c> and adjacent periods
/// tile perfectly with no overlap and no hole.
/// </summary>
public readonly record struct DateRange(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    public TimeSpan Duration => End - Start;

    public int TotalDays => (int)Math.Ceiling(Duration.TotalDays);

    public bool Overlaps(DateRange other) => Start < other.End && other.Start < End;
}
