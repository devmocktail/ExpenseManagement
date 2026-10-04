using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillStarterAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        /// <remarks>
        /// Accounts shipped with a starter account seeded at registration, which
        /// silently did nothing for everyone who had already registered. They
        /// opened the app to an account picker that hid itself, a transfer form
        /// that said "you need two accounts", and no way to reach either feature
        /// — the data model was right and the feature was unusable.
        ///
        /// One "Cash in hand" per user who has none, in their own currency.
        /// Idempotent via NOT EXISTS, so re-running it cannot produce a second.
        /// </remarks>
        migrationBuilder.Sql("""
            INSERT INTO "Accounts" (
                "Id", "UserId", "Name", "Type", "CurrencyCode", "OpeningBalance",
                "Icon", "Color", "IsDefault", "IsArchived", "SortOrder",
                "CreatedAt", "IsDeleted")
            SELECT
                gen_random_uuid(),
                u."Id",
                'Cash in hand',
                1,                                     -- AccountType.Cash
                COALESCE(s."CurrencyCode", 'INR'),     -- their own currency, not a guess
                0,
                'wallet-outline',
                '#16A34A',
                true,                                  -- nothing to displace: they have none
                false,
                0,
                now(),
                false
            FROM "Users" u
            -- UserSettings is not soft-deletable: it is one row per user for
            -- the life of the account, so there is no IsDeleted to filter on.
            LEFT JOIN "UserSettings" s ON s."UserId" = u."Id"
            WHERE NOT EXISTS (
                SELECT 1 FROM "Accounts" a
                 WHERE a."UserId" = u."Id" AND a."IsDeleted" = false
            );
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        // Removes only the untouched starter accounts this migration could have
        // created: still named "Cash in hand", still default, and with nothing
        // recorded against them. An account the user has since renamed or spent
        // from is theirs, and dropping it would delete real data to undo a
        // backfill.
        migrationBuilder.Sql("""
            DELETE FROM "Accounts" a
             WHERE a."Name" = 'Cash in hand'
               AND a."IsDefault" = true
               AND a."OpeningBalance" = 0
               AND a."UpdatedAt" IS NULL
               AND NOT EXISTS (SELECT 1 FROM "Transactions" t WHERE t."AccountId" = a."Id")
               AND NOT EXISTS (
                   SELECT 1 FROM "Transfers" tr
                    WHERE tr."FromAccountId" = a."Id" OR tr."ToAccountId" = a."Id");
            """);
        }
    }
}
