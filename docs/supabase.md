# Supabase

Supabase provisions PostgreSQL, which is what this application now targets, so
it is a direct fit — no provider translation layer, no compromises on the
schema. What follows is the part that is not obvious.

## Which connection string, and why it matters

Supabase exposes the same database three ways, and picking the wrong one
produces failures that look nothing like a connection problem.

Find them under **Project Settings → Database → Connection string**.

| Mode | Port | Use it? |
|---|---|---|
| **Direct** | 5432 | Yes, if your host has IPv6. Full protocol support. |
| **Session pooler** | 5432 | **Recommended.** Full protocol support, IPv4 reachable. |
| **Transaction pooler** | 6543 | **No — see below.** |

### Why not the transaction pooler

The transaction pooler hands your connection a different backend between
statements. Npgsql relies on session state that does not survive that:

* **Prepared statements.** Npgsql prepares and caches statements per
  connection. In transaction mode the prepared statement may not exist on
  whichever backend serves the next call, and you get
  `prepared statement "_p1" does not exist` — sporadically, under load, and
  never in testing.
* **Explicit transactions.** Registration, category reassignment, account
  closure and refresh-token rotation all open real transactions. Transaction
  mode is workable for those but leaves no room for the session-scoped state
  around them.

If you must use port 6543, disable automatic preparation:

```
...;Max Auto Prepare=0;No Reset On Close=true
```

Simpler to use the session pooler and skip the whole class of problem.

### Converting Supabase's URI to Npgsql's format

Supabase gives you a URI. Npgsql accepts key/value pairs, so translate it:

```
postgresql://postgres.abcdefgh:PASSWORD@aws-0-eu-west-2.pooler.supabase.com:5432/postgres
```

becomes

```
Host=aws-0-eu-west-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abcdefgh;Password=PASSWORD;SSL Mode=Require;Trust Server Certificate=true
```

Three things people get wrong here:

* **Username includes the project ref** — `postgres.abcdefgh`, not `postgres`.
* **Database is `postgres`**, not your project name.
* **`SSL Mode=Require` is mandatory.** Supabase refuses unencrypted connections.
  `Trust Server Certificate=true` avoids shipping Supabase's CA chain; drop it
  and pin the certificate properly if you would rather verify it.

If the password contains `;` or `=`, wrap the value in single quotes:
`Password='pa;ss=word'`.

## Deploying the API to Render against Supabase

Render still hosts the API; Supabase only replaces the database. In the Render
dashboard set:

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | the converted string above |
| `Jwt__Secret` | 32+ random chars — `openssl rand -base64 48` |
| `Hosting__BehindTlsTerminatingProxy` | `true` |
| `Database__MigrateOnStartup` | `true` |
| `Seed__DemoUser` | `false` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

## Applying the schema

With `Database__MigrateOnStartup=true` the container migrates itself on first
boot. To do it yourself instead:

```bash
cd backend
ConnectionStrings__DefaultConnection="Host=...;Password=..." \
  dotnet ef database update \
    --project ExpenseManagement.Infrastructure \
    --startup-project ExpenseManagement.Api
```

The migration creates its tables in the `public` schema alongside Supabase's own
(`auth`, `storage`, `realtime`). They do not collide.

## What this app does NOT use from Supabase

Worth stating, because the overlap invites confusion:

* **Supabase Auth is unused.** Identity, password hashing, JWT issuing and
  refresh-token rotation are all handled by ASP.NET Core Identity in this API.
  Supabase's `auth.users` table stays empty. Two auth systems over one database
  is a way to end up with two half-correct ones.
* **Row Level Security is unused.** Isolation is enforced by the DbContext's
  global query filters, described in the root README. RLS would be a second
  mechanism enforcing the same rule, and the two would drift.
* **PostgREST / the Supabase client is unused.** Everything goes through this
  API, which is what applies validation, ownership checks and audit logging.
  Pointing the mobile app at PostgREST directly would bypass all three.

Supabase here is a hosted PostgreSQL with a good dashboard. That is a
legitimate way to use it.

## Notes on the port from SQL Server

Behaviour that changed, and what was done about it:

* **`LIKE` is case-sensitive on PostgreSQL.** SQL Server's default collation is
  not, so transaction search silently stopped matching. Both sides of the
  comparison are now lowered in `TransactionService`.
* **Category names likewise.** `Name == name` was a case-insensitive duplicate
  check on SQL Server and a case-sensitive one here, which would have allowed
  "Food" and "food" to coexist. `CategoryService` now compares on `lower()`.
* **`NEWSEQUENTIALID()` is gone**, replaced by `gen_random_uuid()`. Sequential
  keys mattered on SQL Server because the primary key is the clustered index
  there and random keys fragment the table itself. PostgreSQL heap tables are
  not ordered by their primary key, so the cost is some B-tree locality on
  insert and nothing more.
* **`SET NULL` is back.** SQL Server counts it as a cascade action and rejects
  a table reachable by two cascade paths from one principal, which forced
  `ClientSetNull` on `Transaction → RecurringTransaction`. PostgreSQL has no
  such rule, so the database enforces it again instead of EF.
* **Booleans are real booleans.** Every `[IsDeleted] = 0` in a filtered index or
  CHECK constraint became `"IsDeleted" = false`; on PostgreSQL comparing a
  boolean to an integer is a type error, not a false result.
* **The colour CHECK was rewritten.** It used T-SQL `LIKE '[0-9A-Fa-f]'`
  character classes, which PostgreSQL's `LIKE` does not implement — it would
  have matched those characters literally and let malformed colours through. It
  is now the regex `"Color" ~ '^#[0-9A-Fa-f]{6}$'`.
