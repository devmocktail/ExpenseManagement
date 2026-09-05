using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// The persistence surface the application layer is allowed to touch.
///
/// EF Core's DbContext already is a Unit of Work and its DbSets already are
/// repositories, so there is deliberately no repository/UoW layer on top —
/// that would only re-expose the same API with fewer capabilities. This
/// interface exists purely so services can be unit-tested against a fake.
///
/// Note what is absent: no <c>Users</c> set. Account mutation goes through
/// ASP.NET Core Identity's UserManager so password hashing and security
/// stamps are never bypassed.
/// </summary>
public interface IAppDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<RecurringTransaction> RecurringTransactions { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<UserSettings> UserSettings { get; }
    DbSet<DeviceToken> DeviceTokens { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a transaction for multi-step writes that must be atomic
    /// (e.g. generating a recurring transaction and advancing its schedule).
    /// </summary>
    Task<IAppDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Provider-agnostic handle over an open database transaction.</summary>
public interface IAppDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
