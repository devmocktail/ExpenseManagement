/*
    Invariant checks for PostgreSQL.

    Every query should return ZERO rows. Anything that comes back is a bug the
    application let through — a safety net for the rules the code claims to
    enforce, not a substitute for the constraints that enforce them.

    Read-only. Safe against any environment.

        psql -h localhost -p 5432 -U postgres -d ExpenseManagement -f verify-integrity.sql
*/

\echo '=== Each section should report 0 rows ==='

\echo ''
\echo '1. Non-positive amounts'
-- Direction is carried by Type, never by the sign of Amount.
SELECT 'Transactions' AS source, "Id", "Amount" FROM "Transactions"          WHERE "Amount" <= 0
UNION ALL
SELECT 'Budgets',                 "Id", "Amount" FROM "Budgets"               WHERE "Amount" <= 0
UNION ALL
SELECT 'RecurringTransactions',   "Id", "Amount" FROM "RecurringTransactions" WHERE "Amount" <= 0;

\echo ''
\echo '2. Transactions referencing another user''s category'
-- If this ever returns a row, the ownership check in TransactionService has a
-- hole and one account can read another's category name through its own ledger.
SELECT t."Id" AS transaction_id, t."UserId" AS txn_owner, c."UserId" AS category_owner
FROM "Transactions" t
JOIN "Categories" c ON c."Id" = t."CategoryId"
WHERE t."UserId" <> c."UserId";

\echo ''
\echo '2b. Budgets referencing another user''s category'
SELECT b."Id", b."UserId" AS budget_owner, c."UserId" AS category_owner
FROM "Budgets" b
JOIN "Categories" c ON c."Id" = b."CategoryId"
WHERE b."CategoryId" IS NOT NULL AND b."UserId" <> c."UserId";

\echo ''
\echo '2c. Receipts referencing another user''s transaction'
SELECT r."Id", r."UserId" AS receipt_owner, t."UserId" AS txn_owner
FROM "Receipts" r
JOIN "Transactions" t ON t."Id" = r."TransactionId"
WHERE r."UserId" <> t."UserId";

\echo ''
\echo '3. Transaction type disagreeing with its category type'
-- Category Type is frozen precisely so this cannot drift. A mismatch means
-- income is being counted as spending somewhere.
SELECT t."Id", t."Type" AS txn_type, c."Type" AS category_type, c."Name"
FROM "Transactions" t
JOIN "Categories" c ON c."Id" = t."CategoryId"
WHERE t."Type" <> c."Type";

\echo ''
\echo '4. Budget windows that end at or before they start'
SELECT "Id", "Name", "StartDate", "EndDate" FROM "Budgets" WHERE "EndDate" <= "StartDate";

\echo ''
\echo '5. Accounts without exactly one settings row'
-- The client assumes settings always exist; a missing row surfaces as a
-- null-currency crash rather than a default.
SELECT u."Id", u."Email", count(s."Id") AS settings_rows
FROM "Users" u
LEFT JOIN "UserSettings" s ON s."UserId" = u."Id"
WHERE NOT u."IsDeleted"
GROUP BY u."Id", u."Email"
HAVING count(s."Id") <> 1;

\echo ''
\echo '6. Live accounts with no categories'
-- Categories are copied per user at registration; none means registration
-- failed partway and left the account unusable.
SELECT u."Id", u."Email"
FROM "Users" u
WHERE NOT u."IsDeleted"
  AND NOT EXISTS (SELECT 1 FROM "Categories" c WHERE c."UserId" = u."Id" AND NOT c."IsDeleted");

\echo ''
\echo '7. Inconsistent soft-delete markers'
-- IsDeleted and DeletedAt are set together by the DbContext interceptor. One
-- without the other means something wrote the row outside that path.
SELECT 'Transactions' AS source, "Id" FROM "Transactions" WHERE ("IsDeleted" AND "DeletedAt" IS NULL) OR (NOT "IsDeleted" AND "DeletedAt" IS NOT NULL)
UNION ALL
SELECT 'Categories',            "Id" FROM "Categories"   WHERE ("IsDeleted" AND "DeletedAt" IS NULL) OR (NOT "IsDeleted" AND "DeletedAt" IS NOT NULL)
UNION ALL
SELECT 'Budgets',               "Id" FROM "Budgets"      WHERE ("IsDeleted" AND "DeletedAt" IS NULL) OR (NOT "IsDeleted" AND "DeletedAt" IS NOT NULL);

\echo ''
\echo '8. Duplicate offline idempotency keys within one account'
-- This is what makes a retried upload after a dropped connection update rather
-- than duplicate. A collision means one spend was recorded twice.
SELECT "UserId", "ClientReference", count(*) AS copies
FROM "Transactions"
WHERE "ClientReference" IS NOT NULL AND NOT "IsDeleted"
GROUP BY "UserId", "ClientReference"
HAVING count(*) > 1;

\echo ''
\echo '9. Duplicate notification keys within one account'
SELECT "UserId", "DeduplicationKey", count(*) AS copies
FROM "Notifications"
WHERE NOT "IsDeleted"
GROUP BY "UserId", "DeduplicationKey"
HAVING count(*) > 1;

\echo ''
\echo '10. Refresh tokens revoked without a reason'
SELECT "Id", "UserId", "RevokedAt" FROM "RefreshTokens"
WHERE "RevokedAt" IS NOT NULL AND "RevokedReason" IS NULL;

\echo ''
\echo '10b. Refresh tokens replaced by themselves'
SELECT "Id", "UserId" FROM "RefreshTokens" WHERE "ReplacedByTokenHash" = "TokenHash";

\echo ''
\echo '11. Malformed currency codes'
SELECT 'Transactions' AS source, "Id", "CurrencyCode" FROM "Transactions" WHERE "CurrencyCode" !~ '^[A-Z]{3}$'
UNION ALL
SELECT 'UserSettings',           "Id", "CurrencyCode" FROM "UserSettings" WHERE "CurrencyCode" !~ '^[A-Z]{3}$';

\echo ''
\echo '12. Category names differing only by case within one account'
-- The expression index UX_Categories_UserId_NameLower_Type should make this
-- impossible. PostgreSQL, unlike SQL Server, compares text case-sensitively by
-- default, so without that index "Food" and "food" would both be storable.
SELECT "UserId", lower("Name") AS name_lower, "Type", count(*) AS variants
FROM "Categories"
WHERE NOT "IsDeleted"
GROUP BY "UserId", lower("Name"), "Type"
HAVING count(*) > 1;

\echo ''
\echo '=== Done. Any rows above are defects. ==='
