using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Application.Features.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers.V1;

/// <summary>
/// Aggregated views over the signed-in user's transactions: totals, charts and
/// category breakdowns for a week, a month or a year.
///
/// Every endpoint takes the same <see cref="AnalyticsQuery"/> — a period and an
/// optional anchor instant — and never a user id. The window is resolved in the
/// caller's own time zone by the service, from the identity on the token.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/analytics")]
[Produces("application/json")]
public class AnalyticsController(IAnalyticsService service) : ControllerBase
{
    /// <summary>
    /// Everything the analytics screen needs in one round trip: summary totals,
    /// the trend series, the full category breakdown, the top categories and the
    /// insights.
    /// </summary>
    /// <remarks>
    /// Named /summary for the client's <c>analyticsApi.full()</c>, but it
    /// deliberately returns the composed response rather than the summary alone —
    /// five requests to paint one screen is five chances to render a half-loaded
    /// dashboard.
    /// </remarks>
    /// <param name="query">Period (week, month or year) and the optional anchor instant.</param>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<AnalyticsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AnalyticsResponseDto>>> GetSummary(
        [FromQuery] AnalyticsQuery query)
    {
        var result = await service.GetFullAsync(query.Period, query.At, HttpContext.RequestAborted);

        return Ok(ApiResponse<AnalyticsResponseDto>.Ok(result));
    }

    /// <summary>
    /// Income and expense per bucket — a day for week and month periods, a month
    /// for year — including empty buckets.
    /// </summary>
    /// <param name="query">Period (week, month or year) and the optional anchor instant.</param>
    [HttpGet("trends")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TrendPointDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrendPointDto>>>> GetTrends(
        [FromQuery] AnalyticsQuery query)
    {
        var result = await service.GetTrendAsync(query.Period, query.At, HttpContext.RequestAborted);

        return Ok(ApiResponse<IReadOnlyList<TrendPointDto>>.Ok(result));
    }

    /// <summary>
    /// Expenses per category for the period, largest first, each with its share
    /// of the period total.
    /// </summary>
    /// <param name="query">Period (week, month or year) and the optional anchor instant.</param>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryBreakdownItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryBreakdownItemDto>>>> GetCategories(
        [FromQuery] AnalyticsQuery query)
    {
        var result = await service.GetCategoryBreakdownAsync(
            query.Period,
            query.At,
            HttpContext.RequestAborted);

        return Ok(ApiResponse<IReadOnlyList<CategoryBreakdownItemDto>>.Ok(result));
    }

    /// <summary>
    /// The same buckets as <c>/trends</c>, for the two-series income-versus-expense
    /// comparison chart.
    /// </summary>
    /// <param name="query">Period (week, month or year) and the optional anchor instant.</param>
    [HttpGet("income-vs-expense")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TrendPointDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrendPointDto>>>> GetIncomeVsExpense(
        [FromQuery] AnalyticsQuery query)
    {
        var result = await service.GetIncomeVsExpenseAsync(
            query.Period,
            query.At,
            HttpContext.RequestAborted);

        return Ok(ApiResponse<IReadOnlyList<TrendPointDto>>.Ok(result));
    }
}
