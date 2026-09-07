using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", t =>
        {
            // The retry backoff is computed from this counter, so a negative value
            // would push the next attempt into the past and spin the delivery worker.
            t.HasCheckConstraint(
                "CK_Notifications_DeliveryAttempts_NonNegative",
                "\"DeliveryAttempts\" >= 0");

            // IsRead and ReadAt are two representations of one fact, and the unread
            // badge trusts the flag while the UI timestamps from the column. Pinning
            // them together stops a partial update leaving a "read" row with no time.
            t.HasCheckConstraint(
                "CK_Notifications_Read_Consistent",
                "(\"IsRead\" = false AND \"ReadAt\" IS NULL) OR (\"IsRead\" = true AND \"ReadAt\" IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Type).HasConversion<byte>().IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasMaxLength(1000)
            .IsRequired();

        // Bounded rather than nvarchar(max): the payload is a handful of ids for
        // deep-linking, and keeping it in-row means the list query does not pull
        // LOB pages for every notification it renders.
        builder.Property(x => x.DataJson).HasMaxLength(2000);

        builder.Property(x => x.IsRead).IsRequired();
        builder.Property(x => x.ReadAt);

        builder.Property(x => x.ScheduledFor).IsRequired();
        builder.Property(x => x.SentAt);

        builder.Property(x => x.DeliveryAttempts).IsRequired();

        // Provider errors are truncated to something diagnosable; the full response
        // belongs in the log sink, not in a row every list query scans past.
        builder.Property(x => x.LastDeliveryError).HasMaxLength(500);

        builder.Property(x => x.DeduplicationKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        // Notifications are worthless once the account is gone, and this is the
        // entity's only FK, so the cascade from Users is the single path into the
        // table and stays legal. WithMany() without an inverse collection is
        // deliberate: an account accumulates thousands of these, and exposing
        // user.Notifications invites a load of the whole history to show a badge.
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The anti-double-notify guarantee: the scheduler is at-least-once, so a
        // re-run must collide here rather than send twice. Filtered on IsDeleted so
        // a dismissed-and-purged notification does not permanently burn its key —
        // the row is only tombstoned, never physically removed.
        builder.HasIndex(x => new { x.UserId, x.DeduplicationKey })
            .HasDatabaseName("UX_Notifications_UserId_DeduplicationKey")
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // The delivery worker asks "what is due, across every account", so UserId is
        // deliberately not the leading column. The filter keeps the index to the
        // pending backlog — a few hundred rows — instead of every notification ever
        // sent, so the queue scan stays cheap as the table grows without bound.
        builder.HasIndex(x => x.ScheduledFor)
            .HasDatabaseName("IX_Notifications_ScheduledFor")
            .HasFilter("\"SentAt\" IS NULL AND \"IsDeleted\" = false")
            .IncludeProperties(x => new { x.UserId, x.Type, x.DeliveryAttempts });

        // The in-app list and the unread badge: filter by user, split on read state,
        // newest first. Descending on CreatedAt matches the sort so the seek returns
        // rows already ordered, and the carried columns make the first page a
        // covering read.
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt })
            .HasDatabaseName("IX_Notifications_UserId_IsRead_CreatedAt")
            .IsDescending(false, false, true)
            .IncludeProperties(x => new { x.Title, x.Type, x.SentAt, x.IsDeleted });
    }
}
