namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// Writes security-relevant events to the audit trail. Implementations must
/// never persist credentials, tokens or full PII — only what is needed to
/// answer "who did what, when, from where".
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        Guid? userId = null,
        string? entityName = null,
        Guid? entityId = null,
        bool succeeded = true,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
