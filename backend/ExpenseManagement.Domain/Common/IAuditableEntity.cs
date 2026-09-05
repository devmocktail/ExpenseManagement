namespace ExpenseManagement.Domain.Common;

/// <summary>
/// Timestamps maintained centrally by the DbContext's SaveChanges interceptor —
/// never set these by hand in a service.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
}
