# Database

The schema is owned by **EF Core migrations**, not by scripts in this folder.
`ExpenseManagement.Infrastructure/Persistence/Migrations` is the source of
truth; everything here is for inspecting a running database.

Never hand-edit the schema. A change applied here and not in a migration exists
on one machine and nowhere else.

## Connecting

Development runs **PostgreSQL 17** locally; deployment uses **Supabase**.

| | Local | Supabase |
|---|---|---|
| Host | `localhost` | `aws-0-<region>.pooler.supabase.com` |
| Port | `5432` | `5432` (session pooler — **not** 6543) |
| Database | `ExpenseManagement` | `postgres` |
| Username | `postgres` | `postgres.<project-ref>` |
| SSL | not required | `SSL Mode=Require` |

See [../docs/supabase.md](../docs/supabase.md) for why the transaction pooler on
6543 breaks Npgsql.

```bash
psql -h localhost -p 5432 -U postgres -d ExpenseManagement
```

## Applying migrations

```bash
cd backend
dotnet ef database update --project ExpenseManagement.Infrastructure                           --startup-project ExpenseManagement.Api
```

The API also migrates on startup in Development, and elsewhere only when
`Database__MigrateOnStartup=true`.

## Files

| File | Purpose |
|---|---|
| `inspect.sql` | Read-only: schema shape, indexes, constraints, row counts |
| `verify-integrity.sql` | Invariants the app depends on. Read-only. |
| `reset-dev.sql` | **Destructive.** Drops and recreates the dev database. |

## Querying directly

Every user-owned table is filtered at the application layer by `AppDbContext`'s
global query filters — one on `IsDeleted`, one on `UserId`. SQL applies neither.
A raw `SELECT * FROM "Transactions"` returns soft-deleted rows **and every
user's data**. Correct at the SQL level, but it will not match what a signed-in
user sees:

```sql
SELECT * FROM "Transactions" WHERE "UserId" = :user_id AND NOT "IsDeleted";
```

Identifiers are quoted because EF creates them in PascalCase. Unquoted,
PostgreSQL folds to lowercase and `SELECT * FROM Transactions` fails with
`relation "transactions" does not exist`.
