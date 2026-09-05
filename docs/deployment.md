# Deployment

## The constraint that shapes everything else

**Render does not offer managed SQL Server.** It provisions PostgreSQL and
Key Value; there is no SQL Server option. This application needs SQL Server —
the EF Core provider, `NEWSEQUENTIALID()` defaults, filtered unique indexes and
`datetimeoffset` columns are all SQL Server specific.

So deploying the API to Render works, but the database has to live somewhere
else. Three honest options:

### 1. Azure SQL Database — recommended

The only one of the three that is both cheap and operationally sane. Microsoft
offers a free serverless tier; check current limits before relying on it, as
they change. Serverless auto-pauses when idle, which means **the first request
after a pause can take 30–60 seconds** while the database resumes. Budget for
that in any health check or client timeout.

Connection string shape:

```
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=ExpenseManagement;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;
```

`Encrypt=True` is required. `Connection Timeout=60` gives a paused serverless
database time to wake rather than failing the first request of the day.

Add Render's outbound IPs to the Azure SQL firewall, or allow Azure services
plus the specific addresses Render lists for your region.

### 2. SQL Server in a container you host

`mcr.microsoft.com/mssql/server` needs roughly **2 GB of RAM**, which rules out
Render's free tier. On a paid instance it needs a persistent disk as well —
without one, every deploy destroys the data. Workable, but you are now
operating a database server.

### 3. Migrate to PostgreSQL

Not a configuration change. It means swapping the EF provider, replacing the
`NEWSEQUENTIALID()` defaults, revisiting filtered indexes, and re-generating
every migration. Reasonable if you would rather stay entirely on Render;
significant work, and it should be a deliberate decision rather than a
deployment workaround.

---

## Deploying to Render

### 1. Provision the database first

Get a SQL Server instance and its connection string before touching Render.
The API's health check includes a database probe, so a service deployed without
a reachable database will fail health checks and restart in a loop.

### 2. Create the service

Either commit `render.yaml` and use **New → Blueprint**, or create a Web Service
manually:

| Setting | Value |
|---|---|
| Runtime | Docker |
| Dockerfile path | `./Dockerfile` |
| Docker build context | `.` (repository root) |
| Health check path | `/health` |

The Dockerfile lives at the **repository root**, not in `backend/`, because
that is where Render looks by default. The build context must be the root too —
the build copies from `backend/`.

### 3. Environment variables

Set these in the dashboard. The two marked **required** have no defaults and
startup fails fast without them, which is deliberate.

| Variable | Value | Why |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | your SQL Server string | **Required** |
| `Jwt__Secret` | 32+ random characters | **Required.** Startup refuses a shorter key — HMAC-SHA256's strength is capped by key length, and a short one is brute-forceable offline from a single captured token |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `Hosting__BehindTlsTerminatingProxy` | `true` | Stops the redirect loop described below |
| `Database__MigrateOnStartup` | `true` | No pre-deploy hook on the free plan |
| `Seed__DemoUser` | `false` | Never seed a known password on a public host |
| `Jwt__Issuer` | `ExpenseManagement.Api` | Must match what the client expects |
| `Jwt__Audience` | `ExpenseManagement.Mobile` | |

Generate a signing key with:

```bash
openssl rand -base64 48
```

The double underscore is ASP.NET Core's nesting separator: `Jwt__Secret` binds
to the `Jwt:Secret` configuration key.

### 4. Point the mobile app at it

```
EXPO_PUBLIC_API_BASE_URL=https://<your-service>.onrender.com
```

No trailing slash and no `/api/v1` — the client appends the version segment.

---

## Failures worth knowing about in advance

### `failed to read dockerfile: open Dockerfile: no such file or directory`

Render looked for a Dockerfile at the repository root and did not find one.
Either the file is missing, or **Docker build context** is set to a
subdirectory. Context must be `.` here.

### Infinite redirects (`ERR_TOO_MANY_REDIRECTS`)

Render terminates TLS at its edge and forwards plain HTTP to the container.
`UseHttpsRedirection()` sees an HTTP request, issues a 307 to `https://`, the
proxy forwards that back as HTTP, and round it goes.

Set `Hosting__BehindTlsTerminatingProxy=true`. `Program.cs` also calls
`UseForwardedHeaders` before anything reads the scheme, so `X-Forwarded-Proto`
and `X-Forwarded-For` are honoured.

That call clears `KnownNetworks` and `KnownProxies`, which means the app trusts
whatever fronts it. Correct behind a PaaS proxy that is the only route in;
**not** correct if the container is ever exposed directly, because then a
client could forge its own `X-Forwarded-For` and defeat the per-IP rate limit.

### Everyone shares one rate-limit bucket

The symptom of missing forwarded headers: `RemoteIpAddress` is the proxy's, so
every unauthenticated caller lands in the same partition and ten people trying
to log in trip the limiter. Fixed by the same `UseForwardedHeaders` call.

### Health check fails immediately after deploy

`/health` includes `AddDbContextCheck`, so it fails when the database is
unreachable. Check the connection string, the firewall, and — on Azure SQL
serverless — whether the database is paused. That is a real failure being
reported honestly, not a false alarm; do not "fix" it by removing the check.

### Uploaded receipts vanish

Render's filesystem is ephemeral. Every deploy and every restart wipes
`/app/storage`. Attach a persistent disk, or implement `IFileStorage` against
object storage. The interface exists precisely so this swap does not touch a
single caller.

### First request after idle takes ~50 seconds

Render's free tier spins a service down after inactivity, and Azure SQL
serverless auto-pauses independently. Both cold starts can stack. Nothing is
broken; the free tier is doing what it says.

---

## Running the whole stack locally with Docker

```bash
cat > .env <<'EOF'
MSSQL_SA_PASSWORD=Your_Strong_Passw0rd!
JWT_SECRET=at-least-32-characters-of-random-text
EOF

docker compose up --build
```

API on `http://localhost:5165`, SQL Server on `localhost,1433`.

Compose waits for SQL Server's health check before starting the API — the
container reports "running" well before it accepts queries, and a migration
fired into that gap fails with a connection error that reads like a
configuration bug.

> **Untested.** Docker was not installed on the machine this was written on, so
> `docker-compose.yml` and `Dockerfile` have never been executed. The .NET build
> they wrap is verified; the container plumbing is not.

---

## Before this is genuinely production-ready

Not done yet, listed so the gap is explicit:

- **No CI.** `.github/workflows` is empty. Nothing builds or tests on push.
- **No automated tests.** The test projects exist and are empty.
- **Migrations run from the app.** Acceptable on a single free-tier instance,
  not acceptable once you scale past one — two instances starting together can
  race on the same schema change.
- **Receipts are on local disk.** Needs object storage before real use.
- **No structured log sink.** Serilog writes to stdout, which Render captures
  and drops after its retention window. Ship them somewhere if you need history.
- **No backups configured.** Whatever hosts the database needs a backup policy;
  Azure SQL has automatic backups, a self-hosted container does not.
