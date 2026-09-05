namespace ExpenseManagement.Application.Common.Models;

/// <summary>
/// Base for any paged query. The setters clamp rather than throw so a client
/// sending <c>?pageSize=100000</c> gets a capped page instead of a 400 — and
/// the server can never be made to materialise an unbounded result set.
/// </summary>
public abstract class PaginationRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value,
        };
    }

    public int Skip => (Page - 1) * PageSize;
}
