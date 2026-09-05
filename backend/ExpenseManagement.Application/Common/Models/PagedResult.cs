namespace ExpenseManagement.Application.Common.Models;

/// <summary>
/// One page of results plus the counters the client needs to drive infinite
/// scroll. Nothing in this API ever returns an unbounded collection.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PagedResult<T> Empty(int page, int pageSize) => new()
    {
        Items = [],
        Page = page,
        PageSize = pageSize,
        TotalCount = 0,
    };
}
