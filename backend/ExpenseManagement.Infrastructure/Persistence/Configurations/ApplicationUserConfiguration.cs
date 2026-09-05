using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Deliberately the nameless ToTable overload: AppDbContext.RenameIdentityTables
        // maps this type to "Users", and naming it twice lets the two drift apart.
        builder.ToTable(t =>
        {
            // The audit pipeline stamps IsDeleted and DeletedAt together, but a support
            // script or a migration can set one without the other, and a row flagged
            // deleted with no DeletedAt is invisible to the app yet never ages into the
            // retention purge — it would linger forever, holding its email hostage.
            t.HasCheckConstraint(
                "CK_Users_SoftDelete_Consistent",
                "([IsDeleted] = 0 AND [DeletedAt] IS NULL) OR ([IsDeleted] = 1 AND [DeletedAt] IS NOT NULL)");

            // FullName initialises to string.Empty, so NOT NULL alone still admits a
            // blank name, which then renders as an empty caption on every screen that
            // greets the user or shows an avatar fallback.
            t.HasCheckConstraint("CK_Users_FullName_NotEmpty", "LEN(LTRIM([FullName])) > 0");
        });

        // Nothing here touches Id, UserName, Email, PasswordHash, the stamps or
        // Identity's UserNameIndex/EmailIndex — IdentityDbContext owns those, and the
        // key is Identity-generated rather than NEWSEQUENTIALID(). Worth knowing: a
        // soft-deleted account keeps occupying its normalised email in those unique
        // indexes, which is intended — the address must not be re-registerable by
        // someone else while the old account's financial history is still on disk.

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        // An opaque IFileStorage key (container + partition + guid + extension), never
        // a signed URL, which is what keeps 400 a safe ceiling instead of NVARCHAR(MAX).
        builder.Property(x => x.AvatarStorageKey)
            .HasMaxLength(400);

        builder.Property(x => x.LastLoginAt);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        // The one-to-one to Settings is configured from the dependent side in
        // UserSettingsConfiguration, which owns the FK column, its Cascade behaviour
        // and the unique index on UserId. Restating it here would give EF Core two
        // conflicting descriptions of the same relationship. The owned collections
        // (Transactions, Budgets, RefreshTokens, ...) are likewise configured by each
        // dependent's own configuration, where the cascade decision belongs.

        // No index on IsDeleted: the SoftDelete global filter appends IsDeleted = 0 to
        // every query, and that predicate matches virtually the whole table, so the
        // optimiser would scan rather than seek it. The two filtered indexes below
        // cover the sparse populations that genuinely need one.

        // Retention purge: hard-deletes accounts whose grace window has expired. Sitting
        // on the rare IsDeleted = 1 side, it stays a few pages regardless of user count.
        builder.HasIndex(x => x.DeletedAt)
            .HasDatabaseName("IX_Users_DeletedAt")
            .HasFilter("[IsDeleted] = 1");

        // Dormancy and re-engagement jobs sweep live accounts by last sign-in; filtering
        // out deleted rows keeps them out of a scan that is otherwise range-only.
        // NULL sorts first on SQL Server, so never-signed-in accounts are found by the
        // same seek that finds the longest-dormant ones.
        builder.HasIndex(x => x.LastLoginAt)
            .HasDatabaseName("IX_Users_LastLoginAt")
            .HasFilter("[IsDeleted] = 0")
            .IncludeProperties(x => new { x.FullName, x.CreatedAt });
    }
}
