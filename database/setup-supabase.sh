#!/usr/bin/env bash
#
# One command to point this project at Supabase and get the schema in.
#
#   bash database/setup-supabase.sh
#
# It prompts for the connection string, writes it to the gitignored
# appsettings.Local.json, applies the migrations, verifies what landed, and
# prints the exact values to paste into Render.
#
# The string is read with `read -rs`: not echoed to the terminal, not written to
# shell history, and redacted in everything this script prints.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_SETTINGS="$REPO_ROOT/backend/ExpenseManagement.Api/appsettings.Local.json"

cat <<'INTRO'
================================================================
  Supabase setup
================================================================

In the Supabase dashboard:

  1. Click  Connect  (green button, top of the page)
  2. Choose  Session pooler        <- NOT the transaction pooler
  3. Choose  .NET  in the dropdown <- gives Npgsql's format directly
  4. Copy the string, and replace [YOUR-PASSWORD] with your real password

It should look like:

  Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;
  Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require

INTRO

printf 'Paste the connection string (input is hidden), then press Enter:\n> '
read -rs CONN
echo ""
echo ""

if [ -z "$CONN" ]; then
  echo "Nothing entered. Run the script again when you have the string."
  exit 1
fi

redact() { printf '%s' "$1" | sed -E 's/(Password=)[^;]*/\1********/I'; }

# --- sanity checks, before anything is written or run --------------------
problems=0

case "$CONN" in
  *[Hh]ost=*) ;;
  *)
    echo "PROBLEM: no Host= in the string."
    echo "         This looks like Supabase's URI form rather than the .NET one."
    echo "         Pick '.NET' in the dropdown, or convert it:"
    echo "           postgresql://USER:PASS@HOST:5432/postgres"
    echo "           -> Host=HOST;Port=5432;Database=postgres;Username=USER;Password=PASS"
    problems=1
    ;;
esac

case "$CONN" in
  *6543*)
    echo "PROBLEM: port 6543 is the TRANSACTION pooler."
    echo "         Npgsql's prepared statements do not survive it - you would get"
    echo "         'prepared statement \"_p1\" does not exist' sporadically under"
    echo "         load and never in testing. Use the session pooler on 5432."
    problems=1
    ;;
esac

case "$CONN" in
  *YOUR-PASSWORD*|*'<password>'*|*'[YOUR-PASSWORD]'*)
    echo "PROBLEM: the placeholder is still in the string."
    echo "         Replace [YOUR-PASSWORD] with your actual database password."
    problems=1
    ;;
esac

case "$CONN" in
  *[Ss][Ss][Ll]*) ;;
  *)
    echo "NOTE: no SSL Mode found. Supabase requires encryption, so appending it."
    CONN="$CONN;SSL Mode=Require;Trust Server Certificate=true"
    ;;
esac

if [ "$problems" -ne 0 ]; then
  echo ""
  echo "Nothing was written or run. Fix the above and try again."
  exit 1
fi

echo "Using: $(redact "$CONN")"
echo ""

# --- write the gitignored local settings ---------------------------------
echo "=== Writing appsettings.Local.json (gitignored) ==="

if [ -f "$LOCAL_SETTINGS" ]; then
  cp "$LOCAL_SETTINGS" "$LOCAL_SETTINGS.bak"
  echo "  existing file backed up to appsettings.Local.json.bak"
fi

# Written with python so the string is JSON-escaped properly - a password
# containing a quote or a backslash would otherwise produce invalid JSON.
CONN="$CONN" python -c '
import json, os, sys
path = sys.argv[1]
with open(path, "w", encoding="utf-8") as handle:
    json.dump({
        "//": "Gitignored. Real credentials live here, never in appsettings.Development.json, which is committed.",
        "ConnectionStrings": {"DefaultConnection": os.environ["CONN"]},
        "Seed": {"DemoUser": False},
    }, handle, indent=2)
    handle.write("\n")
' "$LOCAL_SETTINGS"

# Belt and braces: confirm git really is ignoring it before going further.
if git -C "$REPO_ROOT" check-ignore -q "$LOCAL_SETTINGS"; then
  echo "  confirmed gitignored - it cannot be committed"
else
  echo ""
  echo "  STOP: appsettings.Local.json is NOT gitignored on this checkout."
  echo "  Deleting it rather than risk committing a live password."
  rm -f "$LOCAL_SETTINGS"
  exit 1
fi

echo ""
echo "  Seed:DemoUser is set to false. A shared database should not get a"
echo "  demo account with a published password."
echo ""

# --- apply and verify -----------------------------------------------------
bash "$REPO_ROOT/database/deploy-to-supabase.sh" "$CONN"

echo ""
cat <<'DONE'
================================================================
  Local setup is done.
================================================================

The connection string is in appsettings.Local.json, which is gitignored, so
`dotnet run` and `dotnet ef` now both talk to Supabase.

To run the API against it:

    cd backend && dotnet run --project ExpenseManagement.Api

To smoke-test it (39 checks):

    python backend/tests/api-smoke/smoke.py http://localhost:5165

For Render, set the SAME string as the environment variable
ConnectionStrings__DefaultConnection - never in a file - plus Jwt__Secret,
Hosting__BehindTlsTerminatingProxy=true and Seed__DemoUser=false.
DONE
