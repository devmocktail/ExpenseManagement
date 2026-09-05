using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("Receipts", t =>
        {
            // The same 10 MB ceiling the upload validator enforces, restated in the
            // database so a bug in the API — or any future path that writes metadata
            // without going through the validator — cannot record a row whose blob
            // the storage tier already refused. The lower bound catches the other
            // failure mode: a zero-byte row left behind by an aborted upload, which
            // would render as a broken thumbnail forever.
            t.HasCheckConstraint(
                "CK_Receipts_FileSizeBytes_Range",
                "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 10485760");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.TransactionId).IsRequired();

        // 260 is the classic Windows MAX_PATH: long enough for any filename a client
        // can actually produce, short enough that the column stays index-friendly.
        // Display only — the value never reaches the filesystem, so length is the
        // only constraint worth enforcing here.
        builder.Property(x => x.FileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FileSizeBytes).IsRequired();

        // Null until the image is probed; the client uses them to reserve layout
        // space before the blob loads, and a receipt is still usable without them.
        builder.Property(x => x.Width);
        builder.Property(x => x.Height);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        // A receipt has no meaning apart from the transaction it evidences, so the
        // transaction owns its lifetime. This is the one cascading path into this
        // table.
        builder.HasOne(x => x.Transaction)
            .WithMany(t => t.Receipts)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, and deliberately not Cascade: Transactions already cascade from
        // Users, so a second cascading Users -> Receipts edge would give SQL Server
        // two cascade paths to the same principal and fail at migration time. Purging
        // an account still removes these rows — they travel out on the
        // Users -> Transactions -> Receipts cascade, within the same delete — while
        // the UserId column keeps the ownership query filter a single-table predicate
        // instead of a join through Transactions.
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two rows pointing at one blob would make deletion unsafe: purging the
        // "deleted" receipt would break the surviving one. Intentionally *not*
        // filtered on IsDeleted — a soft-deleted row still references the blob until
        // the retention job removes it, so a tombstone must keep reserving its key.
        builder.HasIndex(x => x.StorageKey)
            .HasDatabaseName("UX_Receipts_StorageKey")
            .IsUnique();

        // Every read is scoped by the ownership filter and then by transaction: the
        // detail screen's attachment strip and the "does this expense have proof?"
        // badge on the list. Carrying the display columns keeps both a covering seek.
        builder.HasIndex(x => new { x.UserId, x.TransactionId })
            .HasDatabaseName("IX_Receipts_UserId_TransactionId")
            .IncludeProperties(x => new { x.FileName, x.ContentType, x.FileSizeBytes, x.IsDeleted });

        // Backs the FK itself. Without a TransactionId-leading index the cascade and
        // the retention job's orphan sweep both degrade to a table scan, since the
        // composite above cannot seek on its second column alone.
        builder.HasIndex(x => x.TransactionId)
            .HasDatabaseName("IX_Receipts_TransactionId");
    }
}
