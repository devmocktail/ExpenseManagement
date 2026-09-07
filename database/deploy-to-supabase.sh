#!/usr/bin/env bash
#
# Applies the EF Core migrations to a Supabase (or any) PostgreSQL database and
# verifies the result.
#
#   ./deploy-to-supabase.sh "Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
#
# or, to keep the string out of your shell history:
#
#   read -rs SUPABASE_CONN && export SUPABASE_CONN
#   ./deploy-to-supabase.sh
#
# The connection string is never echoed, never written to a file, and never
# committed. It is passed to dotnet-ef through the environment.

set -euo pipefail

CONN="${1:-${SUPABASE_CONN:-}}"

if [ -z "$CONN" ]; then
  cat <<'USAGE'
No connection string supplied.

Get it from Supabase: Project Settings -> Database -> Connection string.
Take the SESSION POOLER on port 5432, not the transaction pooler on 6543 --
the transaction pooler swaps backends between statements, so Npgsql's prepared
statements disappear and you get "prepared statement _p1 does not exist"
sporadically under load and never in testing.

Supabase gives you a URI:

  postgresql://postgres.abcdefgh:PASSWORD@aws-0-eu-west-2.pooler.supabase.com:5432/postgres

Npgsql wants key/value pairs:

  Host=aws-0-eu-west-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abcdefgh;Password=PASSWORD;SSL Mode=Require;Trust Server Certificate=true

Note: the username includes the project ref, the database is "postgres", and
SSL Mode=Require is mandatory -- Supabase refuses unencrypted connections.

Then:
  ./deploy-to-supabase.sh "Host=...;..."
USAGE
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND="$REPO_ROOT/backend"

# Redact the password whenever anything about the target is printed.
safe_conn() { printf '%s' "$CONN" | sed -E 's/(Password=)[^;]*/\1********/I'; }

echo "=== Target ==="
echo "  $(safe_conn)"
echo ""

case "$CONN" in
  *6543*)
    echo "WARNING: port 6543 is Supabase's TRANSACTION pooler."
    echo "         Npgsql's prepared statements do not survive it. Use the"
    echo "         session pooler on 5432, or add ';Max Auto Prepare=0' and"
    echo "         ';No Reset On Close=true' and accept the caveats."
    echo ""
    ;;
esac

case "$CONN" in
  *[Ss][Ss][Ll]*) ;;
  *)
    echo "WARNING: no SSL Mode in the connection string. Supabase requires"
    echo "         'SSL Mode=Require'. This will almost certainly fail."
    echo ""
    ;;
esac

echo "=== 1/3  Applying migrations ==="

# Restore first. On a fresh clone there is no obj/project.assets.json, and
# dotnet-ef reports that as "Unable to retrieve project metadata. Ensure it's an
# SDK-style project" -- which sounds like the project is malformed rather than
# simply un-restored.
dotnet restore "$BACKEND" >/dev/null 2>&1 || {
  echo "  restore failed; running it again with output:"
  dotnet restore "$BACKEND"
  exit 1
}


# --no-build is deliberately NOT used. dotnet-ef defaults to the Debug
# configuration, so a --no-build run loads whatever happens to be sitting in
# bin/Debug. After the SQL Server to PostgreSQL port that directory still held
# Microsoft.EntityFrameworkCore.SqlServer.dll, and the run failed with
# "Keyword not supported: 'host'" against a perfectly valid Npgsql connection
# string -- a message that points nowhere near its actual cause. Building costs
# a few seconds and removes the whole failure mode.
ConnectionStrings__DefaultConnection="$CONN" \
  dotnet ef database update \
    --project "$BACKEND/ExpenseManagement.Infrastructure" \
    --startup-project "$BACKEND/ExpenseManagement.Api" \
    2>&1 | grep -vE "^Build started|^Determining|Entity Framework tools version" || true

echo ""
echo "=== 2/3  Verifying the schema landed ==="

# psql is not required -- dotnet-ef already proved connectivity. If it is
# available, use it for a real look at what was created.
if command -v psql >/dev/null 2>&1; then
  # Npgsql key/value -> libpq URI, so psql can reuse the same credentials.
  host=$(printf '%s' "$CONN" | grep -oiE 'Host=[^;]*' | cut -d= -f2)
  port=$(printf '%s' "$CONN" | grep -oiE 'Port=[^;]*' | cut -d= -f2)
  db=$(printf '%s' "$CONN" | grep -oiE 'Database=[^;]*' | cut -d= -f2)
  user=$(printf '%s' "$CONN" | grep -oiE 'Username=[^;]*' | cut -d= -f2)
  pass=$(printf '%s' "$CONN" | grep -oiE 'Password=[^;]*' | cut -d= -f2)

  export PGPASSWORD="$pass"
  PSQL=(psql -h "$host" -p "${port:-5432}" -U "$user" -d "${db:-postgres}" -tAc)

  echo "  tables      : $("${PSQL[@]}" "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';")"
  echo "  constraints : $("${PSQL[@]}" "SELECT count(*) FROM pg_constraint WHERE contype='c' AND conname LIKE 'CK_%';")"
  echo "  indexes     : $("${PSQL[@]}" "SELECT count(*) FROM pg_indexes WHERE schemaname='public';")"
  echo "  migrations  : $("${PSQL[@]}" "SELECT string_agg(\"MigrationId\", ', ') FROM \"__EFMigrationsHistory\";")"

  echo ""
  echo "  money columns (all must be numeric(18,2)):"
  "${PSQL[@]}" "SELECT '    ' || table_name || '.' || column_name || ' -> ' || data_type || '(' || numeric_precision || ',' || numeric_scale || ')' FROM information_schema.columns WHERE table_schema='public' AND column_name='Amount' ORDER BY table_name;"

  echo ""
  echo "  the case-insensitive category index (absent = duplicates by case become possible):"
  found=$("${PSQL[@]}" "SELECT count(*) FROM pg_indexes WHERE indexname='UX_Categories_UserId_NameLower_Type';")
  if [ "$found" = "1" ]; then
    echo "    present"
  else
    echo "    *** MISSING *** — the migration's raw-SQL step did not run"
  fi

  unset PGPASSWORD
else
  echo "  psql not on PATH; skipping the detailed check."
  echo "  Migrations applied without error, which already proves connectivity."
fi

echo ""
echo "=== 3/3  Next ==="
cat <<'NEXT'
  Set these in Render (Environment tab), then redeploy:

    ConnectionStrings__DefaultConnection   the string you just used
    Jwt__Secret                            openssl rand -base64 48
    ASPNETCORE_ENVIRONMENT                 Production
    Hosting__BehindTlsTerminatingProxy     true
    Seed__DemoUser                         false

  Database__MigrateOnStartup can stay false now that the schema is applied
  here. Leaving it true is harmless -- migrations are idempotent -- but it
  means two instances starting together could race on a future schema change.

  Then smoke-test the deployment:

    python backend/tests/api-smoke/smoke.py https://your-service.onrender.com
NEXT
