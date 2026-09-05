# Database

The schema is owned by **EF Core migrations**, not by scripts in this folder.
`ExpenseManagement.Infrastructure/Persistence/Migrations` is the source of
truth; everything here is for inspecting and exercising a running database.

Never hand-edit the schema. A change applied here and not in a migration is a
change that exists on one machine and nowhere else — the next
`dotnet ef database update` will not know about it, and the next developer will
get a different database from yours.

## Connecting

The development database runs on **SQL Server 2025 Express LocalDB**.

| | |
|---|---|
| Server name | `(localdb)\MSSQLLocalDB` |
| Authentication | Windows Authentication |
| Database | `ExpenseManagement` |
| Encryption | Optional (or tick *Trust server certificate*) |

LocalDB starts on demand, but if a tool cannot see it:

```powershell
& "C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe" start MSSQLLocalDB
& "C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe" info  MSSQLLocalDB
```

If `(localdb)\MSSQLLocalDB` still fails to resolve, connect by the named pipe
that `info` prints — LocalDB's pipe name changes every restart, so read it fresh
rather than saving it.

### Command line, no GUI needed

Which `sqlcmd` you have matters, because the two behave differently here.

**go-sqlcmd** (`Microsoft.Sqlcmd` from winget, installs to `C:\Program Files\SqlCmd`)
does *not* resolve the `(localdb)\MSSQLLocalDB` shorthand — it reports
`no named pipe instance matching 'MSSQLLOCALDB'`. Give it the pipe:

```powershell
$localdb = "C:\Program Files\Microsoft SQL Server\170\Tools\Binn\SqlLocalDB.exe"
$pipe = ((& $localdb info MSSQLLocalDB | Where-Object { $_ -match 'Instance pipe name' }) -replace '.*:\s*','')

sqlcmd -S $pipe -d ExpenseManagement -E -C -Q "SELECT COUNT(*) FROM Transactions"
```

Resolve the pipe fresh each time — LocalDB mints a new one on every restart, so
a hard-coded pipe name works until the next reboot and then mystifies you.

**The older ODBC sqlcmd** (shipped under `Client SDK\ODBC\<ver>\Tools\Binn`)
accepts the shorthand directly:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d ExpenseManagement -E -C -Q "SELECT COUNT(*) FROM Transactions"
```

In both, `-E` is Windows auth and `-C` trusts the self-signed certificate
LocalDB uses. SSMS resolves `(localdb)\MSSQLLocalDB` natively and needs none of
this.

## Applying migrations

```powershell
cd backend
dotnet ef database update --project ExpenseManagement.Infrastructure --startup-project ExpenseManagement.Api
```

The API also migrates automatically at startup **in Development only**. Staging
and production apply migrations as a deliberate deployment step, so a rollback
is possible and two instances starting at once cannot race on schema changes.

## Files

| File | Purpose |
|---|---|
| `inspect.sql` | Read-only queries: schema shape, indexes, constraints, row counts |
| `verify-integrity.sql` | Checks the invariants the app depends on. Read-only. |
| `reset-dev.sql` | **Destructive.** Drops the dev database so the next API start rebuilds it. |

## A warning about querying this database directly

Every user-owned table is filtered at the application layer by
`AppDbContext`'s global query filters — one on `IsDeleted`, one on `UserId`.
SQL does not apply those. A raw `SELECT * FROM Transactions` returns

* soft-deleted rows the app treats as gone, and
* **every user's** rows, not one user's.

That is expected and correct at the SQL level, but it means a count taken here
will not match what a signed-in user sees. Filter explicitly:

```sql
SELECT * FROM Transactions
WHERE UserId = @userId AND IsDeleted = 0;
```
