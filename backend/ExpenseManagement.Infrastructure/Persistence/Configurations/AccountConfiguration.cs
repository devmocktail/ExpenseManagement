using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", t =>
        {
            t.HasCheckConstraint(
                "CK_Accounts_Color_Hex",
                "\"Color\" ~ '^#[0-9A-Fa-f]{6}$'");

            // Four digits, as digits. Stops anyone persuading the column to
            // hold a full card number by way of a "label".
            t.HasCheckConstraint(
                "CK_Accounts_Last4_Digits",
                "\"Last4\" IS NULL OR \"Last4\" ~ '^[0-9]{4}$'");

            // OpeningBalance is deliberately unconstrained in sign: a credit
            // card legitimately starts negative.
            t.HasCheckConstraint(
                "CK_Accounts_CurrencyCode_Iso",
                "\"CurrencyCode\" ~ '^[A-Z]{3}$'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasConversion<byte>().IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        // Money is numeric(18,2) everywhere in this system; never float.
        builder.Property(x => x.OpeningBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Institution).HasMaxLength(100);
        builder.Property(x => x.Last4).HasMaxLength(4).IsFixedLength();

        builder.Property(x => x.Icon).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(7).IsFixedLength().IsRequired();

        builder.Property(x => x.IsDefault).IsRequired();
        builder.Property(x => x.IsArchived).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One name per user. Filtered on IsDeleted so a name freed by a soft
        // delete can be reused — the tombstone row is never physically removed
        // and would otherwise block it forever.
        //
        // Case sensitivity: this index is case-SENSITIVE on PostgreSQL, so
        // "Wallet" and "wallet" both pass. AccountService rejects the duplicate
        // and a companion expression index over lower("Name") closes the race;
        // that index is raw SQL in the migration because EF Core has no fluent
        // syntax for an index over an expression.
        builder.HasIndex(x => new { x.UserId, x.Name })
            .HasDatabaseName("UX_Accounts_UserId_Name")
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // At most one default per user. A partial unique index over a constant
        // expresses "only one row may have IsDefault true" exactly, which is
        // otherwise a check the application has to remember on every write.
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("UX_Accounts_UserId_Default")
            .IsUnique()
            .HasFilter("\"IsDefault\" = true AND \"IsDeleted\" = false");

        // Drives the account picker and the accounts screen.
        builder.HasIndex(x => new { x.UserId, x.SortOrder })
            .HasDatabaseName("IX_Accounts_UserId_SortOrder")
            .IncludeProperties(x => new { x.Name, x.Type, x.Icon, x.Color, x.IsArchived, x.IsDeleted });
    }
}
