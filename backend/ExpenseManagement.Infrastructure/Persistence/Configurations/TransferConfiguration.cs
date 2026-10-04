using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers", t =>
        {
            // A transfer to itself is not a no-op, it is a bug that looks like
            // data: it would show in the list as real movement while changing
            // no balance. Rejected by the database so no code path can create one.
            t.HasCheckConstraint(
                "CK_Transfers_Accounts_Differ",
                "\"FromAccountId\" <> \"ToAccountId\"");

            // Direction is carried by the two account ids. A negative amount
            // would be a second, contradictory way to express it.
            t.HasCheckConstraint(
                "CK_Transfers_Amount_Positive",
                "\"Amount\" > 0");

            t.HasCheckConstraint(
                "CK_Transfers_CurrencyCode_Iso",
                "\"CurrencyCode\" ~ '^[A-Z]{3}$'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.TransferDate).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.ClientReference).HasMaxLength(100);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction rather than the Restrict used elsewhere, and the difference
        // matters here.
        //
        // Both FKs point at Accounts, which itself cascades from Users, so
        // deleting a user reaches Transfers by two routes: directly, and
        // through the accounts the transfer references. PostgreSQL checks
        // RESTRICT immediately, so whether that delete succeeds would depend on
        // the order it happens to process the cascades in. NO ACTION is checked
        // at the end of the statement, by which point the transfer rows have
        // been removed by their own cascade and the constraint is satisfied.
        //
        // Orphans are prevented either way: NO ACTION still refuses to leave a
        // transfer pointing at a row that no longer exists. In normal operation
        // none of this fires, because closing an account is a soft delete.
        builder.HasOne(x => x.FromAccount)
            .WithMany(a => a.TransfersOut)
            .HasForeignKey(x => x.FromAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ToAccount)
            .WithMany(a => a.TransfersIn)
            .HasForeignKey(x => x.ToAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        // The transfers list: by user, newest first.
        builder.HasIndex(x => new { x.UserId, x.TransferDate })
            .HasDatabaseName("IX_Transfers_UserId_TransferDate")
            .IsDescending(false, true)
            .IncludeProperties(x => new { x.FromAccountId, x.ToAccountId, x.Amount, x.CurrencyCode, x.IsDeleted });

        // Balance derivation sums transfers per account in both directions.
        // Without these, computing one account's balance scans every transfer
        // the user has ever made.
        builder.HasIndex(x => new { x.FromAccountId, x.TransferDate })
            .HasDatabaseName("IX_Transfers_FromAccountId_TransferDate")
            .IncludeProperties(x => new { x.Amount, x.IsDeleted });

        builder.HasIndex(x => new { x.ToAccountId, x.TransferDate })
            .HasDatabaseName("IX_Transfers_ToAccountId_TransferDate")
            .IncludeProperties(x => new { x.Amount, x.IsDeleted });

        // Idempotency for offline sync. A retried upload must not move the
        // money a second time.
        builder.HasIndex(x => new { x.UserId, x.ClientReference })
            .HasDatabaseName("UX_Transfers_UserId_ClientReference")
            .IsUnique()
            .HasFilter("\"ClientReference\" IS NOT NULL AND \"IsDeleted\" = false");
    }
}
