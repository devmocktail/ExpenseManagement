/*
    Read-only inspection of the ExpenseManagement schema.

    Nothing here writes. Run the whole file, or one section at a time in SSMS
    with the section highlighted.

    USE ExpenseManagement;
*/

-- ---------------------------------------------------------------------------
-- 1. What is actually in the database
-- ---------------------------------------------------------------------------
SELECT
    t.name                                   AS TableName,
    SUM(p.rows)                              AS [Rows],
    CAST(SUM(a.total_pages) * 8.0 / 1024 AS decimal(10, 2)) AS SizeMB
FROM sys.tables t
JOIN sys.partitions p  ON p.object_id = t.object_id AND p.index_id IN (0, 1)
JOIN sys.allocation_units a ON a.container_id = p.partition_id
GROUP BY t.name
ORDER BY SUM(p.rows) DESC;


-- ---------------------------------------------------------------------------
-- 2. Money columns
--
-- Every one of these must be decimal(18,2). A float here would silently lose
-- fractions of a rupee on every arithmetic operation, and the loss compounds.
-- ---------------------------------------------------------------------------
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    CASE
        WHEN DATA_TYPE = 'decimal' AND NUMERIC_PRECISION = 18 AND NUMERIC_SCALE = 2
            THEN 'OK'
        ELSE '*** WRONG TYPE FOR MONEY ***'
    END AS Verdict
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('Amount', 'Spent', 'Balance')
   OR DATA_TYPE IN ('float', 'real', 'money', 'smallmoney')
ORDER BY TABLE_NAME, COLUMN_NAME;


-- ---------------------------------------------------------------------------
-- 3. Foreign keys and their delete behaviour
--
-- SQL Server allows at most ONE cascade path between the same pair of tables.
-- Every user-owned table already cascades from Users, so a second cascading
-- path into the same table is rejected at CREATE time. Receipts is the
-- interesting row: it cascades from Transactions and is NO_ACTION from Users.
-- ---------------------------------------------------------------------------
SELECT
    OBJECT_NAME(fk.parent_object_id)     AS ChildTable,
    c1.name                              AS ChildColumn,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
    fk.delete_referential_action_desc    AS OnDelete
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c1 ON c1.object_id = fkc.parent_object_id
                   AND c1.column_id = fkc.parent_column_id
ORDER BY ChildTable, ParentTable;


-- ---------------------------------------------------------------------------
-- 4. Indexes
--
-- The workhorse is IX_Transactions_UserId_TransactionDate: the list screen,
-- the dashboard and every analytics range scan all filter by user and order by
-- date. IX_Transactions_CategoryId exists only so SQL Server can enforce the
-- RESTRICT on category deletes without scanning the largest table.
-- ---------------------------------------------------------------------------
SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name                   AS IndexName,
    i.type_desc              AS IndexType,
    i.is_unique              AS IsUnique,
    i.filter_definition      AS FilteredOn,
    STUFF((
        SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
        FROM sys.index_columns ic
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id
          AND ic.index_id = i.index_id
          AND ic.is_included_column = 0
        ORDER BY ic.key_ordinal
        FOR XML PATH('')), 1, 2, '') AS KeyColumns
FROM sys.indexes i
WHERE i.object_id IN (SELECT object_id FROM sys.tables)
  AND i.type > 0
ORDER BY TableName, IndexName;


-- ---------------------------------------------------------------------------
-- 5. CHECK constraints
--
-- These are the last line of defence. The API validates the same rules, but a
-- bug there must not be able to write a negative amount or a budget window
-- that ends before it starts.
-- ---------------------------------------------------------------------------
SELECT
    OBJECT_NAME(parent_object_id) AS TableName,
    name                          AS ConstraintName,
    definition                    AS Rule
FROM sys.check_constraints
ORDER BY TableName, name;


-- ---------------------------------------------------------------------------
-- 6. Accounts
--
-- Note this deliberately shows soft-deleted accounts too: the application hides
-- them, SQL does not.
-- ---------------------------------------------------------------------------
SELECT
    u.Email,
    u.FullName,
    u.IsDeleted,
    u.CreatedAt,
    u.LastLoginAt,
    (SELECT COUNT(*) FROM Transactions t WHERE t.UserId = u.Id AND t.IsDeleted = 0) AS LiveTransactions,
    (SELECT COUNT(*) FROM Categories  c WHERE c.UserId = u.Id AND c.IsDeleted = 0) AS Categories,
    (SELECT COUNT(*) FROM Budgets     b WHERE b.UserId = u.Id AND b.IsDeleted = 0) AS Budgets
FROM Users u
ORDER BY u.CreatedAt;


-- ---------------------------------------------------------------------------
-- 7. The demo account's ledger
-- ---------------------------------------------------------------------------
SELECT TOP (25)
    t.TransactionDate,
    CASE t.Type WHEN 1 THEN 'Expense' WHEN 2 THEN 'Income' END AS Type,
    t.Amount,
    t.CurrencyCode,
    c.Name AS Category,
    t.Merchant,
    t.Description
FROM Transactions t
JOIN Categories c ON c.Id = t.CategoryId
JOIN Users      u ON u.Id = t.UserId
WHERE u.Email = 'demo@expense.local'
  AND t.IsDeleted = 0
ORDER BY t.TransactionDate DESC;


-- ---------------------------------------------------------------------------
-- 8. Spend by category for the demo account
--
-- This is the same aggregation AnalyticsService performs; running it here is a
-- quick way to confirm the API's numbers against the raw data.
-- ---------------------------------------------------------------------------
SELECT
    c.Name                                     AS Category,
    COUNT(*)                                   AS Txns,
    SUM(t.Amount)                              AS Total,
    CAST(100.0 * SUM(t.Amount) / NULLIF(SUM(SUM(t.Amount)) OVER (), 0) AS decimal(5, 2)) AS Pct
FROM Transactions t
JOIN Categories c ON c.Id = t.CategoryId
JOIN Users      u ON u.Id = t.UserId
WHERE u.Email = 'demo@expense.local'
  AND t.IsDeleted = 0
  AND t.Type = 1                                   -- expenses only
GROUP BY c.Name
ORDER BY Total DESC;


-- ---------------------------------------------------------------------------
-- 9. Refresh-token families
--
-- Only the SHA-256 hash is ever stored, so nothing here is replayable. A row
-- with ReplacedByTokenHash set has been rotated; presenting it again is treated
-- as theft and revokes the whole family.
-- ---------------------------------------------------------------------------
SELECT
    u.Email,
    rt.FamilyId,
    rt.CreatedAt,
    rt.ExpiresAt,
    rt.RevokedAt,
    rt.RevokedReason,
    CASE WHEN rt.ReplacedByTokenHash IS NULL THEN 'current' ELSE 'rotated' END AS State
FROM RefreshTokens rt
JOIN Users u ON u.Id = rt.UserId
ORDER BY u.Email, rt.FamilyId, rt.CreatedAt;


-- ---------------------------------------------------------------------------
-- 10. Audit trail
--
-- Append-only, and deliberately has no foreign key to Users: it must survive
-- account deletion and must be able to record a failed login for an address
-- that never became an account.
-- ---------------------------------------------------------------------------
SELECT TOP (30)
    CreatedAt,
    Action,
    Succeeded,
    UserId,
    IpAddress,
    MetadataJson
FROM AuditLogs
ORDER BY CreatedAt DESC;
