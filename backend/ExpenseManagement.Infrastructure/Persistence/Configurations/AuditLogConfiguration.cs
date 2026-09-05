using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", t =>
        {
            // The table is append-only: an audit row that can be edited after the
            // fact is not evidence. SaveChanges only stamps UpdatedAt on a
            // Modified entry, so pinning it to NULL turns any future attempt to
            // rewrite history into a constraint violation instead of a silent edit.
            t.HasCheckConstraint("CK_AuditLogs_AppendOnly", "[UpdatedAt] IS NULL");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        // No foreign key to Users, on purpose. The trail has to outlive the account
        // it describes — a GDPR erasure or a hard delete must not take the record of
        // what that account did with it — and UserId is also written for principals
        // that never existed, such as a failed login against an unknown email. An FK
        // would make both of those impossible.
        builder.Property(x => x.UserId);

        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(100);
        builder.Property(x => x.EntityId);

        // 45 chars is the widest textual IP: an IPv4-mapped IPv6 address.
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(400);

        builder.Property(x => x.Succeeded).IsRequired();

        // Capped rather than NVARCHAR(MAX) so the column stays a normal
        // variable-length value: SQL Server can push it to row-overflow when a row
        // gets fat, but ordinary reads stay in-row instead of chasing LOB pointers.
        builder.Property(x => x.MetadataJson).HasMaxLength(4000);

        builder.Property(x => x.CreatedAt).IsRequired();

        // Deliberately no global soft-delete or ownership filter is added here, and
        // none is applied by the context either: AuditLog implements neither marker
        // interface, because admins and incident response read across every account.
        // Access is gated by authorization policy at the API edge, not by the model.

        // "What did this user do, most recent first" — the account-activity screen and
        // every incident investigation. Not filtered to UserId IS NOT NULL: the NULL
        // rows are exactly the anonymous failed logins that get scanned during a
        // credential-stuffing review, and they must stay seekable by date.
        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_UserId_CreatedAt")
            .IsDescending(false, true);

        // "How many of this event happened in this window" — lockout heuristics and
        // the security dashboard aggregate by action over a time range across all users.
        builder.HasIndex(x => new { x.Action, x.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_Action_CreatedAt")
            .IsDescending(false, true)
            .IncludeProperties(x => new { x.Succeeded, x.IpAddress });

        // "Who touched this receipt/transaction" — a per-record trail. Filtered
        // because most events are account-level and carry no entity reference; the
        // filter keeps this index a fraction of the table's size.
        builder.HasIndex(x => new { x.EntityName, x.EntityId, x.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_EntityName_EntityId_CreatedAt")
            .HasFilter("[EntityId] IS NOT NULL")
            .IsDescending(false, false, true);
    }
}
