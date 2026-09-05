using System.Linq.Expressions;
using System.Reflection;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ExpenseManagement.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context, and the place multi-user isolation is
/// actually enforced.
///
/// Every user-owned entity carries two named global query filters:
/// <c>SoftDelete</c> and <c>UserOwnership</c>. The ownership filter compares
/// UserId against the id taken from the JWT — never from the request body or
/// route — so a service that forgets its own WHERE clause still cannot read
/// another account's rows. When nobody is authenticated the filter parameter is
/// null, and comparing a non-null column to NULL matches nothing: the failure
/// mode is "returns no rows", not "returns everything".
/// </summary>
public class AppDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IAppDbContext
{
    private readonly IDateTimeProvider? _clock;

    /// <summary>
    /// Captured once per request. Exposed as a property rather than a local so
    /// EF Core re-reads it per query and lifts it into a SQL parameter, which
    /// keeps one compiled plan shared across every user instead of one per id.
    /// </summary>
    public Guid? CurrentUserId { get; }

    /// <summary>Design-time and test constructor: no ambient user, so every filtered query returns nothing.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUser currentUser,
        IDateTimeProvider clock) : base(options)
    {
        _clock = clock;
        CurrentUserId = currentUser.UserId;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        RenameIdentityTables(builder);
        ApplyGlobalFilters(builder);
        ApplyDecimalPrecisionFallback(builder);
    }

    /// <summary>
    /// Identity's default names (AspNetUsers, AspNetRoles, ...) leak the
    /// framework into the schema and read poorly beside the domain tables.
    /// </summary>
    private static void RenameIdentityTables(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
    }

    private void ApplyGlobalFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (entityType.IsOwned() || clrType.IsAbstract) continue;

            // ApplicationUser is deliberately exempt from the SoftDelete filter
            // even though it implements ISoftDeletable.
            //
            // It is the required principal of seven relationships. Filtering it
            // makes every one of those navigations "required, but sometimes
            // filtered away", which EF flags as
            // PossibleIncorrectRequiredNavigationWithQueryFilterInteraction —
            // and the underlying behaviour is real: an Include(x => x.User)
            // becomes an INNER JOIN that silently drops a closed account's rows
            // from a background job's results instead of failing loudly.
            //
            // A closed account is instead blocked at the only door that matters:
            // AuthService refuses to issue tokens for a user whose IsDeleted is
            // set, and revokes their refresh-token families on closure. Because
            // no token is ever issued, the UserOwnership filter on their data
            // can never match a request either.
            if (clrType == typeof(ApplicationUser))
            {
                continue;
            }

            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);
            var isUserOwned = typeof(IUserOwnedEntity).IsAssignableFrom(clrType);

            if (!isSoftDeletable && !isUserOwned) continue;

            var parameter = Expression.Parameter(clrType, "e");
            var entity = builder.Entity(clrType);

            if (isSoftDeletable)
            {
                // e => !e.IsDeleted
                var body = Expression.Not(
                    Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

                entity.HasQueryFilter(
                    FilterNames.SoftDelete,
                    Expression.Lambda(body, parameter));
            }

            if (isUserOwned)
            {
                // e => (Guid?)e.UserId == this.CurrentUserId
                var left = Expression.Convert(
                    Expression.Property(parameter, nameof(IUserOwnedEntity.UserId)),
                    typeof(Guid?));

                var right = Expression.Property(
                    Expression.Constant(this),
                    nameof(CurrentUserId));

                entity.HasQueryFilter(
                    FilterNames.UserOwnership,
                    Expression.Lambda(Expression.Equal(left, right), parameter));
            }
        }
    }

    /// <summary>
    /// Belt and braces: a decimal that some configuration forgot to map would
    /// otherwise land on SQL Server's default decimal(18,0) and silently
    /// truncate every amount to whole units.
    /// </summary>
    private static void ApplyDecimalPrecisionFallback(ModelBuilder builder)
    {
        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            if (property.GetColumnType() is null && property.GetPrecision() is null)
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditRules();
        return base.SaveChanges();
    }

    /// <summary>
    /// Stamps CreatedAt/UpdatedAt centrally so no service can forget them, and
    /// converts Remove on a soft-deletable entity into an update — hard-deleting
    /// a transaction would orphan its receipts and rewrite past analytics.
    /// </summary>
    private void ApplyAuditRules()
    {
        var now = _clock?.UtcNow ?? DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = now;
                        auditable.UpdatedAt = null;
                        break;

                    case EntityState.Modified:
                        auditable.UpdatedAt = now;
                        // Guard against a detached graph resetting the original creation time.
                        entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                        break;
                }
            }

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable deletable)
            {
                entry.State = EntityState.Modified;
                deletable.IsDeleted = true;
                deletable.DeletedAt = now;

                if (entry.Entity is IAuditableEntity alsoAuditable)
                {
                    alsoAuditable.UpdatedAt = now;
                }
            }
        }
    }

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    public async Task<IAppDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfDbTransaction(transaction);
    }

    private sealed class EfDbTransaction(IDbContextTransaction inner) : IAppDbTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            inner.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            inner.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}

/// <summary>
/// Names of the global query filters. Background jobs that legitimately need to
/// scan across accounts suspend exactly one — IgnoreQueryFilters([UserOwnership])
/// — which keeps soft-deleted rows excluded and makes every cross-user read a
/// single greppable token in the codebase.
/// </summary>
public static class FilterNames
{
    public const string SoftDelete = "SoftDelete";
    public const string UserOwnership = "UserOwnership";
}
