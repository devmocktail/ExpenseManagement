using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceTokens", t =>
        {
            // A token Expo has reported as unregistered must never be picked up by
            // the sender again. Keeping the two columns consistent in the database
            // means a partial update — or a job that stamps InvalidatedAt but
            // forgets the flag — fails loudly instead of burning push quota on a
            // dead installation forever.
            t.HasCheckConstraint(
                "CK_DeviceTokens_Invalidated_Inactive",
                "\"InvalidatedAt\" IS NULL OR \"IsActive\" = false");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).IsRequired();

        // 200 leaves generous headroom over the ~50-character ExponentPushToken[...]
        // form while staying far inside the 1700-byte limit for a unique index key,
        // which an unbounded nvarchar would blow past.
        builder.Property(x => x.Token)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Platform).HasConversion<byte>().IsRequired();

        builder.Property(x => x.DeviceName).HasMaxLength(100);
        builder.Property(x => x.AppVersion).HasMaxLength(20);

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.LastSeenAt).IsRequired();
        builder.Property(x => x.InvalidatedAt);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // DeviceToken is IUserOwnedEntity but deliberately not ISoftDeletable: a
        // revoked registration carries no history worth keeping and a tombstone
        // would hold the token string hostage against the unique index below.
        // Closing the account therefore hard-deletes the rows, and this single FK
        // is the table's only cascade path into Users, so the cascade is legal.
        builder.HasOne(x => x.User)
            .WithMany(u => u.DeviceTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique across the whole table, not per user: the OS can hand the same
        // token to a different account after a reinstall or a device handover, and
        // a per-user key would let both rows survive and push one person's alerts
        // to somebody else's lock screen. The global key forces registration to
        // move the row (update UserId) rather than insert a duplicate.
        builder.HasIndex(x => x.Token)
            .HasDatabaseName("UX_DeviceTokens_Token")
            .IsUnique();

        // The push fan-out's only read: given a user, get the live tokens. The
        // included columns are exactly what the sender needs to build the Expo
        // payload, so notifying a user never leaves the index.
        builder.HasIndex(x => new { x.UserId, x.IsActive })
            .HasDatabaseName("IX_DeviceTokens_UserId_IsActive")
            .IncludeProperties(x => new { x.Token, x.Platform });

        // Reaper support: the cleanup job scans for installations that stopped
        // checking in. Filtered to live rows because dead ones are deleted
        // outright, which keeps this index a small fraction of the table.
        builder.HasIndex(x => x.LastSeenAt)
            .HasDatabaseName("IX_DeviceTokens_LastSeenAt")
            .HasFilter("\"IsActive\" = true");
    }
}
