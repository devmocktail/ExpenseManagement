/*
    Read-only inspection of the ExpenseManagement schema on PostgreSQL.

    Nothing here writes. Run the whole file, or one statement at a time.

        psql -h localhost -p 5432 -U postgres -d ExpenseManagement -f inspect.sql

    Identifiers are double-quoted throughout because EF creates them in
    PascalCase; unquoted, PostgreSQL folds to lowercase and nothing resolves.
*/

\echo '=== 1. Tables and row counts ==='
SELECT
    c.relname                                   AS table_name,
    c.reltuples::bigint                         AS estimated_rows,
    pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r'
ORDER BY pg_total_relation_size(c.oid) DESC;


\echo ''
\echo '=== 2. Money columns (every one must be numeric(18,2)) ==='
-- A float here would lose fractions of a unit on every operation, and the loss
-- compounds silently across a ledger.
SELECT
    table_name,
    column_name,
    data_type,
    numeric_precision,
    numeric_scale,
    CASE
        WHEN data_type = 'numeric' AND numeric_precision = 18 AND numeric_scale = 2
            THEN 'OK'
        ELSE '*** WRONG TYPE FOR MONEY ***'
    END AS verdict
FROM information_schema.columns
WHERE table_schema = 'public'
  AND (column_name IN ('Amount', 'Spent', 'Balance')
       OR data_type IN ('double precision', 'real', 'money'))
ORDER BY table_name, column_name;


\echo ''
\echo '=== 3. Foreign keys and delete behaviour ==='
-- PostgreSQL, unlike SQL Server, permits several cascade paths to the same
-- principal, which is why Transactions -> RecurringTransactions can be a real
-- SET NULL here rather than being emulated in EF.
SELECT
    con.conrelid::regclass::text  AS child_table,
    con.confrelid::regclass::text AS parent_table,
    con.conname                   AS constraint_name,
    CASE con.confdeltype
        WHEN 'a' THEN 'NO ACTION'
        WHEN 'r' THEN 'RESTRICT'
        WHEN 'c' THEN 'CASCADE'
        WHEN 'n' THEN 'SET NULL'
        WHEN 'd' THEN 'SET DEFAULT'
    END AS on_delete
FROM pg_constraint con
JOIN pg_namespace n ON n.oid = con.connamespace
WHERE con.contype = 'f' AND n.nspname = 'public'
ORDER BY child_table, parent_table;


\echo ''
\echo '=== 4. Indexes ==='
-- The workhorse is IX_Transactions_UserId_TransactionDate: the list screen, the
-- dashboard and every analytics range scan filter by user and order by date.
-- IX_Transactions_CategoryId exists only so the RESTRICT on category deletes
-- can be enforced without scanning the largest table.
SELECT
    tablename  AS table_name,
    indexname  AS index_name,
    indexdef   AS definition
FROM pg_indexes
WHERE schemaname = 'public'
ORDER BY tablename, indexname;


\echo ''
\echo '=== 5. CHECK constraints ==='
-- The last line of defence. The API validates the same rules, but a bug there
-- must not be able to write a negative amount or an inverted budget window.
SELECT
    con.conrelid::regclass::text AS table_name,
    con.conname                  AS constraint_name,
    pg_get_constraintdef(con.oid) AS definition
FROM pg_constraint con
JOIN pg_namespace n ON n.oid = con.connamespace
WHERE con.contype = 'c'
  AND n.nspname = 'public'
  AND con.conname LIKE 'CK_%'
ORDER BY table_name, constraint_name;


\echo ''
\echo '=== 6. Accounts (soft-deleted ones included — SQL applies no filter) ==='
SELECT
    u."Email",
    u."FullName",
    u."IsDeleted",
    u."CreatedAt",
    u."LastLoginAt",
    (SELECT count(*) FROM "Transactions" t WHERE t."UserId" = u."Id" AND NOT t."IsDeleted") AS live_transactions,
    (SELECT count(*) FROM "Categories"  c WHERE c."UserId" = u."Id" AND NOT c."IsDeleted") AS categories,
    (SELECT count(*) FROM "Budgets"     b WHERE b."UserId" = u."Id" AND NOT b."IsDeleted") AS budgets
FROM "Users" u
ORDER BY u."CreatedAt";


\echo ''
\echo '=== 7. The demo account ledger ==='
SELECT
    t."TransactionDate",
    CASE t."Type" WHEN 1 THEN 'Expense' WHEN 2 THEN 'Income' END AS type,
    t."Amount",
    t."CurrencyCode",
    c."Name" AS category,
    t."Merchant"
FROM "Transactions" t
JOIN "Categories" c ON c."Id" = t."CategoryId"
JOIN "Users"      u ON u."Id" = t."UserId"
WHERE u."Email" = 'demo@expense.local' AND NOT t."IsDeleted"
ORDER BY t."TransactionDate" DESC
LIMIT 25;


\echo ''
\echo '=== 8. Spend by category for the demo account ==='
-- The same aggregation AnalyticsService performs, so its output can be checked
-- against the API's.
SELECT
    c."Name"                 AS category,
    count(*)                 AS transactions,
    sum(t."Amount")          AS total,
    round(100.0 * sum(t."Amount") / NULLIF(sum(sum(t."Amount")) OVER (), 0), 2) AS pct
FROM "Transactions" t
JOIN "Categories" c ON c."Id" = t."CategoryId"
JOIN "Users"      u ON u."Id" = t."UserId"
WHERE u."Email" = 'demo@expense.local'
  AND NOT t."IsDeleted"
  AND t."Type" = 1
GROUP BY c."Name"
ORDER BY total DESC;


\echo ''
\echo '=== 9. Refresh-token families ==='
-- Only a SHA-256 hash is ever stored, so nothing here is replayable. A row with
-- ReplacedByTokenHash set has been rotated; presenting it again is treated as
-- theft and revokes the family.
SELECT
    u."Email",
    rt."FamilyId",
    rt."CreatedAt",
    rt."ExpiresAt",
    rt."RevokedAt",
    rt."RevokedReason",
    CASE WHEN rt."ReplacedByTokenHash" IS NULL THEN 'current' ELSE 'rotated' END AS state
FROM "RefreshTokens" rt
JOIN "Users" u ON u."Id" = rt."UserId"
ORDER BY u."Email", rt."FamilyId", rt."CreatedAt";


\echo ''
\echo '=== 10. Audit trail ==='
-- Append-only, and deliberately without a foreign key to Users: it must survive
-- account deletion and record a failed login for an address that never became
-- an account.
SELECT "CreatedAt", "Action", "Succeeded", "UserId", "IpAddress"
FROM "AuditLogs"
ORDER BY "CreatedAt" DESC
LIMIT 30;
