using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", t =>
        {
            // A reason or a successor hash on a row that was never revoked means the
            // rotation write only half-landed, and reuse detection would then read
            // that row as still live. Enforcing the implication in the database makes
            // the bad interleaving fail at write time rather than silently widen a
            // stolen token's validity window.
            t.HasCheckConstraint(
                "CK_RefreshTokens_Revoked_Metadata",
                "[RevokedAt] IS NOT NULL OR ([RevokedReason] IS NULL AND [ReplacedByTokenHash] IS NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.UserId).IsRequired();

        // Base64 of a SHA-256 digest is 44 characters; 100 is headroom for a future
        // algorithm change while keeping the unique index key far inside SQL Server's
        // 1700-byte limit, which an unbounded nvarchar would exceed outright.
        builder.Property(x => x.TokenHash)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FamilyId).IsRequired();

        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.RevokedReason).HasMaxLength(200);

        // Stored as a bare hash rather than a self-referencing FK on purpose: a real
        // FK to RefreshTokens would be a second path into a table that already
        // cascades from Users, and rotation chains would then block account deletion.
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(100);

        // 45 fits the longest IPv6 literal form (IPv4-mapped, "::ffff:255.255.255.255").
        builder.Property(x => x.CreatedByIp).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(400);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // RefreshToken is IUserOwnedEntity but deliberately not ISoftDeletable:
        // revocation is explicit (RevokedAt / RevokedReason), because a spent or
        // expired row must stay directly queryable — a soft-delete filter would hide
        // exactly the rows reuse detection needs to see, turning a replayed stolen
        // token into a cache miss that mints a fresh session. Closing the account is
        // the one case where the rows should genuinely disappear, and this FK is the
        // table's only cascade path into Users, so the cascade is legal.
        builder.HasOne(x => x.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The refresh endpoint's only lookup, and unique because two rows sharing a
        // hash would let a redeemed token authenticate a session that is not its own.
        // Non-nullable, so no filter is needed to keep NULLs from colliding.
        builder.HasIndex(x => x.TokenHash)
            .HasDatabaseName("UX_RefreshTokens_TokenHash")
            .IsUnique();

        // Serves both readers of the same shape: the active-sessions screen (one user,
        // not yet expired) and the reaper's range scan. RevokedAt is included so the
        // liveness test is answered from the index instead of a key lookup per row.
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt })
            .HasDatabaseName("IX_RefreshTokens_UserId_ExpiresAt")
            .IncludeProperties(x => new { x.RevokedAt, x.FamilyId });

        // Reuse detection revokes a whole login chain in a single UPDATE ... WHERE
        // FamilyId = @id, so this must be a seek: it runs on the hot path of a
        // suspected token theft, when the priority is closing the window fast.
        builder.HasIndex(x => x.FamilyId)
            .HasDatabaseName("IX_RefreshTokens_FamilyId");

        builder.Ignore(x => x.IsRevoked);
    }
}
