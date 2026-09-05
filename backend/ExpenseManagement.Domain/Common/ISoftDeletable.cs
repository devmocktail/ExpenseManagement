namespace ExpenseManagement.Domain.Common;

/// <summary>
/// Rows that are hidden rather than removed. A global query filter on the
/// DbContext excludes deleted rows from every query, so services never
/// have to remember to filter.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}
