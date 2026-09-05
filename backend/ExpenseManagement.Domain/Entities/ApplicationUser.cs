using Microsoft.AspNetCore.Identity;
using ExpenseManagement.Domain.Common;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// The account. Extends ASP.NET Core Identity so password hashing, lockout and
/// security stamps come from the framework rather than hand-rolled crypto.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, IAuditableEntity, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Storage key of the avatar image, resolved through IFileStorage. Not a public URL.</summary>
    public string? AvatarStorageKey { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public UserSettings? Settings { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<RecurringTransaction> RecurringTransactions { get; set; } = [];
    public ICollection<DeviceToken> DeviceTokens { get; set; } = [];
}
