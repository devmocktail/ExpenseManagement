using ExpenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseManagement.Infrastructure.Persistence.Configurations;

public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings", t =>
        {
            // Thresholds are percentages of a budget, and the alerting job compares
            // spend against them directly — a 0 or a 150 would either fire on every
            // transaction or never fire at all, with no visible error.
            t.HasCheckConstraint(
                "CK_UserSettings_BudgetWarningThreshold_Range",
                "\"BudgetWarningThreshold\" BETWEEN 1 AND 100");

            t.HasCheckConstraint(
                "CK_UserSettings_BudgetCriticalThreshold_Range",
                "\"BudgetCriticalThreshold\" BETWEEN 1 AND 100");

            // A critical alert that fires before the warning inverts the escalation:
            // the user would be told "critical" first and "warning" afterwards.
            t.HasCheckConstraint(
                "CK_UserSettings_BudgetThresholds_Ordered",
                "\"BudgetCriticalThreshold\" >= \"BudgetWarningThreshold\"");

            // Capped at 28, not 31: a period starting on the 29th-31st has no
            // anchor in February, and the period generator would have to silently
            // pick a different day for some months.
            t.HasCheckConstraint(
                "CK_UserSettings_MonthStartDay_Range",
                "\"MonthStartDay\" BETWEEN 1 AND 28");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        // BCP 47 tags stay short ("en-IN", "pt-BR-Nordestino" at the extreme); IANA
        // zone ids are the long ones ("America/Argentina/Buenos_Aires").
        builder.Property(x => x.Locale).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();

        builder.Property(x => x.Theme).HasConversion<byte>().IsRequired();

        // Deliberately no HasDefaultValue on the flags: EF Core treats a store
        // default plus a CLR-default value as "not set", so a user turning
        // BudgetAlertsEnabled off would have the database write true back.
        // The C# initialisers are the single source of the defaults.
        builder.Property(x => x.BudgetAlertsEnabled).IsRequired();
        builder.Property(x => x.RecurringRemindersEnabled).IsRequired();
        builder.Property(x => x.MonthlySummaryEnabled).IsRequired();
        builder.Property(x => x.BiometricEnabled).IsRequired();

        builder.Property(x => x.BudgetWarningThreshold).IsRequired();
        builder.Property(x => x.BudgetCriticalThreshold).IsRequired();
        builder.Property(x => x.MonthStartDay).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // One row per account, and it carries no financial history — nothing here
        // is worth preserving once the account is gone, so Cascade rather than the
        // Restrict used on ledger tables. This is the entity's only FK, so it is
        // also the only path into Users and cannot trip the multiple-cascade-path
        // rule. UserSettings is not ISoftDeletable for the same reason: a stale
        // preferences row would only ever block the user re-creating settings.
        builder.HasOne(x => x.User)
            .WithOne(u => u.Settings)
            .HasForeignKey<UserSettings>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The one-to-one is enforced in the database, not just in the model: a
        // second settings row would make "the user's currency" ambiguous and the
        // winner would depend on scan order. UserId is non-nullable, so no filter
        // is needed for NULL collisions. The three formatting columns are included
        // because almost every response formats money or dates through them, which
        // turns the per-request settings read into a single index seek.
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("UX_UserSettings_UserId")
            .IsUnique()
            .IncludeProperties(x => new { x.CurrencyCode, x.Locale, x.TimeZoneId });
    }
}
