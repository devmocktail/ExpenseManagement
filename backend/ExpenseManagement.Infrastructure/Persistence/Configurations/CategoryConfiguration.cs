using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", t =>
        {
            // The client renders swatches straight from this value, so a malformed
            // colour is a visual bug on every screen the category appears on. The
            // pattern pins the exact #RRGGBB shape; a-f is spelled out so the rule
            // still holds if the column is ever moved to a case-sensitive collation.
            t.HasCheckConstraint(
                "CK_Categories_Color_Hex",
                "[Color] LIKE '[#][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Type).HasConversion<byte>().IsRequired();

        builder.Property(x => x.Icon)
            .HasMaxLength(50)
            .IsRequired();

        // nchar(7) rather than nvarchar: the value is always exactly "#RRGGBB", and
        // the fixed width means a short string is space-padded into a length the
        // check constraint rejects instead of silently reaching the client.
        builder.Property(x => x.Color)
            .HasMaxLength(7)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.IsSystem).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.DeletedAt);

        // Categories are per-user copies with no shared state, so closing an account
        // takes them with it. This is the entity's only FK, which is what keeps the
        // cascade legal: the dependents that point at a category (transactions,
        // budgets, schedules) already cascade from Users themselves and therefore
        // restrict on their Category FK to avoid a second path into this table.
        builder.HasOne(x => x.User)
            .WithMany(u => u.Categories)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Type is part of the key because "Gift" is a plausible expense *and* income
        // bucket for the same person. Filtered on IsDeleted so a name freed by a
        // soft delete can be reused — without the filter the tombstone row would
        // block it forever, since the row is never physically removed.
        builder.HasIndex(x => new { x.UserId, x.Name, x.Type })
            .HasDatabaseName("UX_Categories_UserId_Name_Type")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Drives the category picker and every chip/legend lookup: filter by user
        // and ledger side, order by SortOrder. Carrying the display columns makes it
        // a covering seek, so opening the picker never touches the base table.
        builder.HasIndex(x => new { x.UserId, x.Type, x.SortOrder })
            .HasDatabaseName("IX_Categories_UserId_Type_SortOrder")
            .IncludeProperties(x => new { x.Name, x.Icon, x.Color, x.IsSystem, x.IsDeleted });
    }
}
