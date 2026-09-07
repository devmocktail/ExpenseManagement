using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets", t =>
        {
            // A zero or negative cap makes every "percent of budget used" figure
            // either meaningless or a division by zero in the alerting job.
            t.HasCheckConstraint("CK_Budgets_Amount_Positive", "\"Amount\" > 0");

            // The window is half-open [Start, End), so an empty or inverted range
            // would be a budget no transaction can ever fall into.
            t.HasCheckConstraint("CK_Budgets_Window_Ordered", "\"EndDate\" > \"StartDate\"");

            // Thresholds are percentages of the cap. Guarded in the database as well
            // as the validator because a 0% threshold would fire an alert on the
            // first rupee and a 500% one would never fire at all.
            t.HasCheckConstraint(
                "CK_Budgets_Thresholds_Range",
                "(\"WarningThreshold\" IS NULL OR (\"WarningThreshold\" BETWEEN 1 AND 100)) AND " +
                "(\"CriticalThreshold\" IS NULL OR (\"CriticalThreshold\" BETWEEN 1 AND 100))");

            // Warning must precede critical, otherwise the escalation ladder inverts
            // and the user is told they are in trouble before they are warned.
            t.HasCheckConstraint(
                "CK_Budgets_Thresholds_Ordered",
                "\"WarningThreshold\" IS NULL OR \"CriticalThreshold\" IS NULL OR " +
                "\"WarningThreshold\" < \"CriticalThreshold\"");

            // Deliberately not capped at 100 like the configured thresholds: this is a
            // high-water mark of what has already been announced, and overspending
            // legitimately pushes it past the cap.
            t.HasCheckConstraint(
                "CK_Budgets_LastNotifiedThreshold_NonNegative",
                "\"LastNotifiedThreshold\" IS NULL OR \"LastNotifiedThreshold\" >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

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

        builder.Property(x => x.Period).HasConversion<byte>().IsRequired();

        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();

        builder.Property(x => x.IsRecurring).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.WarningThreshold);
        builder.Property(x => x.CriticalThreshold);
        builder.Property(x => x.LastNotifiedThreshold);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Budgets)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on two counts. Semantically, deleting a category must not erase the
        // budget history that proves what the user planned to spend, and SetNull would
        // be worse than deleting — it would silently promote a category cap into an
        // account-wide one and distort every past period. Structurally, Categories
        // already cascades from Users, so a cascading Budget -> Category FK would give
        // Budgets a second cascade path to the same principal and SQL Server rejects
        // that at migration time.
        builder.HasOne(x => x.Category)
            .WithMany(c => c.Budgets)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Supports the RESTRICT probe above, which filters on CategoryId alone
        // and so cannot seek any of the UserId-leading indexes. Filtered because
        // an overall budget carries a NULL CategoryId and never participates.
        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("IX_Budgets_CategoryId")
            .HasFilter("\"CategoryId\" IS NOT NULL");

        // "Which budgets cover this date?" is the question the dashboard, the
        // transaction-save hook and the alerting job all ask. Carrying the projected
        // columns keeps that a seek rather than a lookup per matched window.
        builder.HasIndex(x => new { x.UserId, x.StartDate, x.EndDate })
            .HasDatabaseName("IX_Budgets_UserId_StartDate_EndDate")
            .IncludeProperties(x => new { x.Amount, x.CategoryId, x.IsActive, x.IsDeleted });

        // Every saved transaction resolves the cap for its own category before it can
        // decide whether an alert is due; without this it scans the user's whole set.
        builder.HasIndex(x => new { x.UserId, x.CategoryId, x.StartDate })
            .HasDatabaseName("IX_Budgets_UserId_CategoryId_StartDate");

        // Active windows are a small slice of a table that grows one row per period
        // forever. Filtering the index to them keeps the hot path's index pages tiny
        // no matter how many years of closed budgets accumulate.
        builder.HasIndex(x => new { x.UserId, x.EndDate })
            .HasDatabaseName("IX_Budgets_UserId_EndDate_Active")
            .HasFilter("\"IsActive\" = true AND \"IsDeleted\" = false")
            .IncludeProperties(x => new { x.CategoryId, x.Amount, x.LastNotifiedThreshold });

        // One cap per category per window, so the auto-roll job cannot double-open a
        // period after a retry. Filtered to exclude the overall budgets (whose NULL
        // CategoryId SQL Server would otherwise treat as equal to every other NULL)
        // and the soft-deleted rows, which must be free to collide with their
        // replacements.
        builder.HasIndex(x => new { x.UserId, x.CategoryId, x.Period, x.StartDate })
            .HasDatabaseName("UX_Budgets_UserId_CategoryId_Period_StartDate")
            .IsUnique()
            .HasFilter("\"CategoryId\" IS NOT NULL AND \"IsDeleted\" = false");

        // The mirror of the above for the overall budget: at most one per period and
        // cadence, so a monthly and a weekly window may share a start date but two
        // monthly ones may not.
        builder.HasIndex(x => new { x.UserId, x.Period, x.StartDate })
            .HasDatabaseName("UX_Budgets_UserId_Period_StartDate_Overall")
            .IsUnique()
            .HasFilter("\"CategoryId\" IS NULL AND \"IsDeleted\" = false");
    }
}
