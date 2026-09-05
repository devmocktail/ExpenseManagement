namespace ExpenseManagement.Application.Features.Dashboard;

/// <summary>
/// The home screen's single read. The six questions it answers — where am I this
/// period, what did I just spend, on what, how does the week look, how is my
/// budget doing, in which currency — used to be six round trips over a mobile
/// connection; they are one here so the screen paints once.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Builds the dashboard for the period containing <paramref name="at"/>,
    /// resolved in the caller's own time zone and month start day. Null means
    /// "now", which is what the app sends on a cold start; an explicit instant
    /// is how the user pages back through previous months.
    /// </summary>
    Task<DashboardResponseDto> GetAsync(DateTimeOffset? at, CancellationToken cancellationToken);
}
