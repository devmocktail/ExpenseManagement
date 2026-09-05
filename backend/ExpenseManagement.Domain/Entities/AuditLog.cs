using ExpenseManagement.Domain.Common;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// Append-only record of security-relevant events (login, password change,
/// account deletion, receipt access). Deliberately not user-owned-filtered:
/// admins need to read across users, so access is gated by policy instead.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Null for events that happen before a user is known, e.g. a failed login for an unknown email.</summary>
    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public bool Succeeded { get; set; } = true;

    /// <summary>Non-sensitive JSON context. Never contains passwords, tokens or full PII.</summary>
    public string? MetadataJson { get; set; }
}
