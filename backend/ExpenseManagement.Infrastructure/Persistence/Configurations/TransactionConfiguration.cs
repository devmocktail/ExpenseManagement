using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", t =>
        {
            // Enforced in the database as well as the validator: a negative or zero
            // amount would flip a balance the wrong way, and direction is carried by
            // Type, not by the sign of Amount.
            t.HasCheckConstraint("CK_Transactions_Amount_Positive", "\"Amount\" > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Amount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.Type).HasConversion<byte>().IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<byte>().IsRequired();

        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Merchant).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.ClientReference).HasMaxLength(100);

        builder.Property(x => x.TransactionDate).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: deleting a category must never silently take the
        // user's spending history with it. The service forces an explicit
        // reassignment instead.
        builder.HasOne(x => x.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deleting the schedule leaves the transactions it already generated
        // intact — they are real money that was really spent.
        //
        // SetNull, which PostgreSQL enforces itself. Under SQL Server this had
        // to be ClientSetNull: that engine counts ON DELETE SET NULL as a
        // cascading action and refuses a table reachable by two cascade paths
        // from one principal (Users -> Transactions, and
        // Users -> RecurringTransactions -> Transactions), failing at DDL time
        // with Msg 1785. PostgreSQL has no such restriction, so the database
        // can enforce this again instead of relying on EF having loaded the
        // children — which means a hard delete of a schedule no longer needs
        // the caller to null the column first.
        builder.HasOne(x => x.RecurringTransaction)
            .WithMany(r => r.GeneratedTransactions)
            .HasForeignKey(x => x.RecurringTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // The workhorse index: the transaction list, the dashboard and every
        // analytics range scan all filter by user and order by date descending.
        // Covering the columns the list projects keeps it a pure index seek.
        builder.HasIndex(x => new { x.UserId, x.TransactionDate })
            .HasDatabaseName("IX_Transactions_UserId_TransactionDate")
            .IsDescending(false, true)
            .IncludeProperties(x => new { x.Amount, x.Type, x.CategoryId, x.Merchant, x.IsDeleted });

        builder.HasIndex(x => new { x.UserId, x.CategoryId, x.TransactionDate })
            .HasDatabaseName("IX_Transactions_UserId_CategoryId_TransactionDate");

        builder.HasIndex(x => new { x.UserId, x.Type, x.TransactionDate })
            .HasDatabaseName("IX_Transactions_UserId_Type_TransactionDate");

        // Offline sync idempotency. Filtered so the many rows with no client
        // reference (everything created online) do not collide on NULL and do
        // not bloat the index.
        builder.HasIndex(x => new { x.UserId, x.ClientReference })
            .HasDatabaseName("UX_Transactions_UserId_ClientReference")
            .IsUnique()
            .HasFilter("\"ClientReference\" IS NOT NULL AND \"IsDeleted\" = false");

        builder.HasIndex(x => x.RecurringTransactionId)
            .HasDatabaseName("IX_Transactions_RecurringTransactionId")
            .HasFilter("\"RecurringTransactionId\" IS NOT NULL");

        // Not for any application query — this exists so SQL Server can enforce
        // the RESTRICT on Category cheaply. Its probe is a bare
        // "WHERE CategoryId = @id" with no user predicate, which cannot seek the
        // composite indexes above because they all lead with UserId. Without
        // this, deleting one category scans the largest table in the schema.
        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("IX_Transactions_CategoryId");

        builder.Ignore(x => x.SignedAmount);
    }
}
