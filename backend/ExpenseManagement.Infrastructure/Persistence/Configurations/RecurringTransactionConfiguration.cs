using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
    {
        builder.ToTable("RecurringTransactions", t =>
        {
            // Same rule as Transactions: direction lives in Type, so a zero or
            // negative template would generate rows that move a balance the wrong
            // way on every future run, not just once.
            t.HasCheckConstraint("CK_RecurringTransactions_Amount_Positive", "\"Amount\" > 0");

            // Interval 0 would make NextRunDate advance by nothing, so the scheduler
            // would re-fire the same schedule forever on the same tick. A negative
            // interval walks the date backwards, which is the same runaway loop.
            t.HasCheckConstraint("CK_RecurringTransactions_Interval_Positive", "\"Interval\" >= 1");

            // 0 disables the reminder; the upper bound stops a schedule from queueing
            // a notification further out than the shortest supported period, which
            // would fire before the previous occurrence had even been generated.
            t.HasCheckConstraint(
                "CK_RecurringTransactions_ReminderDaysBefore_Range",
                "\"ReminderDaysBefore\" >= 0 AND \"ReminderDaysBefore\" <= 30");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.CategoryId).IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.Type).HasConversion<byte>().IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<byte>().IsRequired();
        builder.Property(x => x.Frequency).HasConversion<byte>().IsRequired();

        builder.Property(x => x.Merchant).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.Property(x => x.Interval).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate);
        builder.Property(x => x.NextRunDate).IsRequired();
        builder.Property(x => x.LastRunDate);
        builder.Property(x => x.OccurrencesGenerated).IsRequired();
        builder.Property(x => x.IsPaused).IsRequired();
        builder.Property(x => x.ReminderDaysBefore).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        // Closing an account takes its schedules with it — a schedule holds no value
        // once nobody can be billed by it, and the transactions it already produced
        // survive independently (Transaction owns that FK with SetNull).
        builder.HasOne(x => x.User)
            .WithMany(u => u.RecurringTransactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict for two reasons. Categories already cascade from Users, so a
        // cascading Category FK here would give SQL Server a second path from Users
        // into this table and the migration would fail outright. It is also the
        // behaviour we want: deleting a category must not silently cancel the rent
        // schedule — the service makes the caller reassign first.
        builder.HasOne(x => x.Category)
            .WithMany(c => c.RecurringTransactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The scheduler's hot path, and the one query in the app that runs across
        // every account: "due schedules, all users". Leading with UserId would force
        // the job to scan the whole table, so NextRunDate leads and the predicate is
        // baked into the index filter — the index then holds only the small live,
        // unpaused set and the job seeks a date range inside it. EndDate is left out
        // of the filter deliberately: it varies per row, so it is a residual check
        // rather than something that can partition the index.
        builder.HasIndex(x => x.NextRunDate)
            .HasDatabaseName("IX_RecurringTransactions_NextRunDate")
            .HasFilter("\"IsPaused\" = false AND \"IsDeleted\" = false");

        // The user's "Recurring" screen, which lists active and paused schedules in
        // separate sections. Carrying the row's display columns keeps the screen a
        // covering seek instead of a lookup per schedule.
        builder.HasIndex(x => new { x.UserId, x.IsPaused })
            .HasDatabaseName("IX_RecurringTransactions_UserId_IsPaused")
            .IncludeProperties(x => new { x.Name, x.Amount, x.NextRunDate, x.Frequency, x.CategoryId, x.IsDeleted });

        // Restrict makes every category delete probe this table for dependents;
        // without an index on the FK that probe is a full scan of all users' rows.
        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("IX_RecurringTransactions_CategoryId");
    }
}
