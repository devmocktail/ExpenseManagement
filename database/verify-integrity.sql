/*
    Invariant checks.

    Every query here should return ZERO rows. Anything that comes back is a bug
    the application let through — this file is a safety net for the rules the
    code claims to enforce, not a substitute for the constraints that enforce
    them.

    Read-only. Safe to run against any environment.

    USE ExpenseManagement;
*/

SET NOCOUNT ON;

PRINT '=== Each section below should report 0 rows ===';


-- ---------------------------------------------------------------------------
-- 1. Money is never negative or zero
--
-- Direction is carried by Type, never by the sign of Amount. A negative amount
-- would flip a balance the wrong way and quietly corrupt every total that row
-- takes part in.
-- ---------------------------------------------------------------------------
PRINT '1. Non-positive amounts';
SELECT 'Transactions' AS Source, Id, Amount FROM Transactions          WHERE Amount <= 0
UNION ALL
SELECT 'Budgets',               Id, Amount FROM Budgets                WHERE Amount <= 0
UNION ALL
SELECT 'RecurringTransactions', Id, Amount FROM RecurringTransactions  WHERE Amount <= 0;


-- ---------------------------------------------------------------------------
-- 2. Nothing is orphaned across users
--
-- A transaction must reference a category belonging to the SAME user. If this
-- ever returns a row, the ownership check in TransactionService has a hole and
-- one account can read another's category name through its own ledger.
-- ---------------------------------------------------------------------------
PRINT '2. Transactions referencing another user''s category';
SELECT t.Id AS TransactionId, t.UserId AS TxnOwner, c.UserId AS CategoryOwner
FROM Transactions t
JOIN Categories c ON c.Id = t.CategoryId
WHERE t.UserId <> c.UserId;

PRINT '2b. Budgets referencing another user''s category';
SELECT b.Id AS BudgetId, b.UserId AS BudgetOwner, c.UserId AS CategoryOwner
FROM Budgets b
JOIN Categories c ON c.Id = b.CategoryId
WHERE b.CategoryId IS NOT NULL AND b.UserId <> c.UserId;

PRINT '2c. Receipts referencing another user''s transaction';
SELECT r.Id AS ReceiptId, r.UserId AS ReceiptOwner, t.UserId AS TxnOwner
FROM Receipts r
JOIN Transactions t ON t.Id = r.TransactionId
WHERE r.UserId <> t.UserId;


-- ---------------------------------------------------------------------------
-- 3. A category's ledger side matches the transactions filed under it
--
-- Type is frozen on a category precisely so this cannot drift. A mismatch means
-- an income row is being counted as spending somewhere.
-- ---------------------------------------------------------------------------
PRINT '3. Transaction type disagreeing with its category type';
SELECT t.Id, t.Type AS TxnType, c.Type AS CategoryType, c.Name
FROM Transactions t
JOIN Categories c ON c.Id = t.CategoryId
WHERE t.Type <> c.Type;


-- ---------------------------------------------------------------------------
-- 4. Budget windows are half-open and ordered
--
-- [Start, End) with End strictly after Start. An inverted window makes
-- "remaining" meaningless and matches no transactions at all.
-- ---------------------------------------------------------------------------
PRINT '4. Budgets whose window ends at or before it starts';
SELECT Id, Name, StartDate, EndDate FROM Budgets WHERE EndDate <= StartDate;


-- ---------------------------------------------------------------------------
-- 5. Every account has exactly one settings row
--
-- The client is written on the assumption that settings always exist, so a
-- missing row surfaces as a null-currency crash rather than a default.
-- ---------------------------------------------------------------------------
PRINT '5. Accounts without exactly one settings row';
SELECT u.Id, u.Email, COUNT(s.Id) AS SettingsRows
FROM Users u
LEFT JOIN UserSettings s ON s.UserId = u.Id
WHERE u.IsDeleted = 0
GROUP BY u.Id, u.Email
HAVING COUNT(s.Id) <> 1;


-- ---------------------------------------------------------------------------
-- 6. Every account has its own categories
--
-- Categories are copied per user at registration; a live account with none
-- means registration failed partway and left the account unusable.
-- ---------------------------------------------------------------------------
PRINT '6. Live accounts with no categories';
SELECT u.Id, u.Email
FROM Users u
WHERE u.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Categories c WHERE c.UserId = u.Id AND c.IsDeleted = 0);


-- ---------------------------------------------------------------------------
-- 7. Soft-delete flags are self-consistent
--
-- IsDeleted and DeletedAt are set together by the DbContext interceptor. One
-- without the other means something wrote the row outside that path.
-- ---------------------------------------------------------------------------
PRINT '7. Inconsistent soft-delete markers';
SELECT 'Transactions' AS Source, Id FROM Transactions WHERE (IsDeleted = 1 AND DeletedAt IS NULL) OR (IsDeleted = 0 AND DeletedAt IS NOT NULL)
UNION ALL
SELECT 'Categories',            Id FROM Categories   WHERE (IsDeleted = 1 AND DeletedAt IS NULL) OR (IsDeleted = 0 AND DeletedAt IS NOT NULL)
UNION ALL
SELECT 'Budgets',               Id FROM Budgets      WHERE (IsDeleted = 1 AND DeletedAt IS NULL) OR (IsDeleted = 0 AND DeletedAt IS NOT NULL);


-- ---------------------------------------------------------------------------
-- 8. Offline idempotency keys are unique per user
--
-- This is what makes a retried upload after a dropped connection update rather
-- than duplicate. A collision here means the same spend was recorded twice.
-- ---------------------------------------------------------------------------
PRINT '8. Duplicate client references within one account';
SELECT UserId, ClientReference, COUNT(*) AS Copies
FROM Transactions
WHERE ClientReference IS NOT NULL AND IsDeleted = 0
GROUP BY UserId, ClientReference
HAVING COUNT(*) > 1;


-- ---------------------------------------------------------------------------
-- 9. Notification de-duplication holds
--
-- The unique index is what stops a user being told twice that they hit 75% of
-- their food budget.
-- ---------------------------------------------------------------------------
PRINT '9. Duplicate notification keys within one account';
SELECT UserId, DeduplicationKey, COUNT(*) AS Copies
FROM Notifications
WHERE IsDeleted = 0
GROUP BY UserId, DeduplicationKey
HAVING COUNT(*) > 1;


-- ---------------------------------------------------------------------------
-- 10. Refresh tokens
--
-- A revoked token must record why, and a token cannot be its own successor.
-- ---------------------------------------------------------------------------
PRINT '10. Refresh tokens revoked without a reason';
SELECT Id, UserId, RevokedAt FROM RefreshTokens WHERE RevokedAt IS NOT NULL AND RevokedReason IS NULL;

PRINT '10b. Refresh tokens replaced by themselves';
SELECT Id, UserId FROM RefreshTokens WHERE ReplacedByTokenHash = TokenHash;


-- ---------------------------------------------------------------------------
-- 11. Stored currency codes are well formed
-- ---------------------------------------------------------------------------
PRINT '11. Malformed currency codes';
SELECT 'Transactions' AS Source, Id, CurrencyCode FROM Transactions WHERE CurrencyCode NOT LIKE '[A-Z][A-Z][A-Z]'
UNION ALL
SELECT 'UserSettings',           Id, CurrencyCode FROM UserSettings WHERE CurrencyCode NOT LIKE '[A-Z][A-Z][A-Z]';


PRINT '=== Done. Any rows above are defects. ===';
