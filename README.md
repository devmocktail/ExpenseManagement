# Expense Management

A personal expense tracker for Android and iOS: an Expo/React Native client
against an ASP.NET Core API on PostgreSQL.

> **Status: in development.** The backend is deployed and verified end to end
> against a real PostgreSQL instance — the 39-check smoke suite passes against
> the live service, not just against localhost. The mobile app typechecks clean
> and has been driven through its main flows in a browser, but is not yet
> pointed at the deployed API. A fast unit/integration suite still does not
> exist — see [What is not done](#what-is-not-done), which is deliberately
> specific so nothing here reads as more finished than it is.

## Stack

| Layer | Choice |
|---|---|
| Mobile | Expo SDK 57, React Native 0.86, TypeScript, expo-router |
| State | TanStack Query (server), Zustand (client) |
| Forms | React Hook Form + Zod |
| Backend | ASP.NET Core 10, C# 13, EF Core 10 |
| Database | PostgreSQL 17 (local in development, Supabase in deployment) |
| Auth | ASP.NET Core Identity, JWT access tokens, rotating refresh tokens |

## Layout

```
backend/     ASP.NET Core solution — Api, Application, Domain, Infrastructure
mobile/      Expo app
database/    Inspection and integrity scripts (schema is owned by EF migrations)
docs/        Architecture, API, security notes
```

The backend is split four ways, and the dependency direction is the point:
`Api → Infrastructure → Application → Domain`. Domain knows about nothing else.

There is **no repository pattern**. EF Core's `DbContext` already is a Unit of
Work and its `DbSet`s already are repositories; wrapping them would re-expose
the same API with fewer capabilities. Services depend on an `IAppDbContext`
interface so they stay testable.

## Running it

### Prerequisites

- **.NET 10 SDK**
- **Node 20.19.4+, 22.13+, or 24.3+** — React Native 0.86 requires it, and Expo's
  env loader calls `util.parseEnv`, which does not exist on earlier versions.
  Node 21.4 fails at `expo start` with
  `TypeError: (0 , _nodeUtil(...).parseEnv) is not a function`.
- **PostgreSQL 17** — locally, or point the connection string at a hosted one

### Backend

```bash
cd backend
dotnet restore
dotnet run --project ExpenseManagement.Api
```

On `Development` it migrates the database and seeds it on startup. Swagger is at
`/swagger`. Staging and production apply migrations as a deliberate deployment
step instead, so a rollback stays possible and two instances starting at once
cannot race on schema changes.

To migrate by hand:

```bash
dotnet ef database update --project ExpenseManagement.Infrastructure \
                          --startup-project ExpenseManagement.Api
```

### Pointing it at Supabase

One command. It prompts for the connection string, writes it to the gitignored
`appsettings.Local.json`, applies the migrations and verifies the result.

```powershell
# Windows PowerShell
.\database\setup-supabase.ps1
```

```bash
# bash / WSL / macOS / Linux
bash database/setup-supabase.sh
```

Both exist because Windows PowerShell 5.1 has neither the `&&` operator nor
`bash` on PATH, so the shell script cannot be run from a default PowerShell
prompt.

See [docs/supabase.md](docs/supabase.md) — in particular, use Supabase's
**session pooler on port 5432**, not the transaction pooler on 6543.

### Mobile

```bash
cd mobile
npm install
npm start          # then press a for Android, i for iOS, w for web
```

`.env.development` points at `http://10.0.2.2:5165`, which is the **Android
emulator's** alias for the host machine's loopback. Change it for your target:

| Target | `EXPO_PUBLIC_API_BASE_URL` |
|---|---|
| Android emulator | `http://10.0.2.2:5165` |
| iOS simulator / web | `http://localhost:5165` |
| Physical device | `http://<your-LAN-IP>:5165` |

Using `localhost` on an Android emulator points at the emulator itself, which is
the most common reason a first run shows "Network Error".

### Demo account

Seeded in Development only, with 30 days of plausible history:

```
demo@expense.local / Demo@Pass123!
```

It requires **both** `ASPNETCORE_ENVIRONMENT=Development` **and**
`Seed:DemoUser=true`. If that flag is ever set in another environment the seeder
logs loudly and refuses, rather than quietly creating a known-password account
on a live system. The password comes from configuration with no fallback.

## Security notes

The parts worth knowing before changing anything:

- **User isolation is enforced in the DbContext**, not in each service. Every
  user-owned entity carries two named global query filters — `SoftDelete` and
  `UserOwnership` — and the ownership filter compares `UserId` against the id
  from the JWT, never from a route or request body. A service that forgets its
  own `WHERE` clause still cannot read another account's rows. With nobody
  authenticated the filter parameter is null and matches nothing, so the failure
  mode is "returns no rows", not "returns everything".
- **Not-found and not-yours both return 404.** Returning 403 for another user's
  row would confirm the row exists, which is an enumeration oracle.
- **Refresh tokens are stored as SHA-256 hashes and rotate on every use.**
  Presenting an already-rotated token is treated as theft and revokes the whole
  token family.
- **Login is timing- and message-identical** for a wrong password, an unknown
  email, and a closed account.
- **Uploads are validated by magic bytes**, not by the client's `Content-Type`
  or filename. Storage keys are server-generated and opaque, so path traversal
  is impossible by construction rather than by sanitising. SVG is refused: it
  can carry script, and serving one from our own origin would be stored XSS.
- **Money is `decimal` end to end**, mapped to `numeric(18,2)`. No float, ever.
- Two cross-user queries exist, both read-only and both named `*Reader`, for the
  background workers that have no authenticated principal. They are the only
  sanctioned uses of `IgnoreQueryFilters`.

### About the committed development secrets

`appsettings.Development.json` contains a JWT signing key and a demo password.
They are committed deliberately so a fresh clone runs with no setup, and they
are useless anywhere else: startup **fails fast** if `Jwt:Secret` is missing or
under 32 characters, and only `Development` reads that file. Staging and
production must supply `Jwt__Secret` and `ConnectionStrings__DefaultConnection`
from the environment. Do not reuse those values for anything.

## What is not done

Stated plainly so the checklist is honest:

- **No unit or integration tests yet.** The xUnit projects exist and are wired
  into the solution but are empty. What does exist is
  `backend/tests/api-smoke/smoke.py`: 39 end-to-end checks against a running API
  covering user isolation, token rotation and reuse detection, money precision,
  case-insensitive search, LIKE escaping and pagination limits. Real evidence,
  but it needs a live API and a database, so it is not the same thing as a fast
  suite that runs on every commit.
- **CI builds but does not meaningfully test.** `.github/workflows/backend-ci.yml`
  compiles from a clean checkout, publishes, and builds the image — which is
  what catches the class of bug where a file is on disk but not in git. Its
  `dotnet test` step passes vacuously because the test projects are empty.
- **iOS is unverified.** Development happened on Windows with no Apple hardware
  and no EAS credentials, so the iOS build has never been produced. Nothing is
  known to be wrong; nothing has been proven right either.
- **`docker-compose.yml` is unverified.** Docker is not installed on the
  development machine, so it has never been executed locally. The `Dockerfile`
  itself is verified: Render builds and runs it, and the resulting container
  serves the live API.
- **Receipts do not survive a redeploy.** `LocalFileStorage` writes to the
  container's filesystem, which on Render is ephemeral. Uploads work and are
  validated, but a deploy discards them. This needs object storage — Supabase
  Storage or S3 — before anyone relies on it.
- **Data-protection keys are ephemeral too**, for the same reason, which the
  deploy log warns about. Nothing currently depends on them surviving a
  restart; anything that later does (persistent antiforgery, encrypted
  cookies) will need a keyring in the database or object storage.
- **Push notifications are unproven.** The scheduling, de-duplication and Expo
  delivery code is written, but it needs a physical device and an EAS project id
  to exercise.
- **Receipt OCR, multi-currency conversion, and shared/family accounts** are not
  implemented. The schema and service boundaries were shaped so they can be
  added without a rewrite.

## Licence

Not yet chosen.
