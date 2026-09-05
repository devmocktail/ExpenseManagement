namespace ExpenseManagement.Domain.Common;

/// <summary>
/// Marks a row as belonging to exactly one user. Combined with the DbContext's
/// global query filter this is the backbone of multi-user data isolation:
/// a query can only ever see rows whose <see cref="UserId"/> matches the
/// authenticated principal.
/// </summary>
public interface IUserOwnedEntity
{
    Guid UserId { get; set; }
}
