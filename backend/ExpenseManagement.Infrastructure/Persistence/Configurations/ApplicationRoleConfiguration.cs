using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures only what this project added to Identity's role entity.
///
/// The table name is set in AppDbContext.RenameIdentityTables; naming it here
/// too would let the two drift apart on a rename.
/// </summary>
public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        // Without an explicit length this lands on NVARCHAR(MAX), which cannot
        // be indexed and forces the row off-page — for what is at most a
        // sentence of admin-facing help text.
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}
