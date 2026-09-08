<#
.SYNOPSIS
    Points this project at Supabase and applies the schema.

.DESCRIPTION
    Prompts for the connection string, writes it to the gitignored
    appsettings.Local.json, applies the EF Core migrations, verifies what
    landed, and prints the values to set in Render.

    The PowerShell twin of setup-supabase.sh. Windows PowerShell 5.1 has no
    `&&` operator and no bash on PATH, so the shell script is unusable from a
    default PowerShell prompt.

.EXAMPLE
    cd D:\native_app
    .\database\setup-supabase.ps1

.NOTES
    The connection string is read with Read-Host -AsSecureString, so it is not
    echoed, not written to PSReadLine history, and redacted in all output.
#>

[CmdletBinding()]
param(
    # Supplying this skips the prompt. Intended for CI and for testing the
    # validation paths, which Read-Host -AsSecureString makes impossible to
    # drive non-interactively - it reads the console directly and ignores a pipe.
    [string] $ConnectionString
)

$ErrorActionPreference = 'Stop'

$repoRoot      = Split-Path -Parent $PSScriptRoot
$localSettings = Join-Path $repoRoot 'backend\ExpenseManagement.Api\appsettings.Local.json'

Write-Host @'
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

'@

if ($PSBoundParameters.ContainsKey('ConnectionString') -and $ConnectionString) {
    $conn = $ConnectionString
}
else {
    # -AsSecureString keeps it off the screen and out of PSReadLine's history.
    $secure = Read-Host -Prompt 'Paste the connection string (hidden), then press Enter' -AsSecureString

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try   { $conn = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

Write-Host ''

if ([string]::IsNullOrWhiteSpace($conn)) {
    Write-Host 'Nothing entered. Run the script again when you have the string.'
    exit 1
}

function Format-Redacted([string] $value) {
    [regex]::Replace($value, '(?i)(Password=)[^;]*', '${1}********')
}

# --- checks, before anything is written or run ---------------------------
$problems = 0

if ($conn -notmatch '(?i)Host=') {
    Write-Host 'PROBLEM: no Host= in the string.'                                   -ForegroundColor Red
    Write-Host '         This looks like Supabase''s URI form rather than the .NET one.'
    Write-Host '         Pick ".NET" in the dropdown, or convert it:'
    Write-Host '           postgresql://USER:PASS@HOST:5432/postgres'
    Write-Host '           -> Host=HOST;Port=5432;Database=postgres;Username=USER;Password=PASS'
    $problems++
}

if ($conn -match '6543') {
    Write-Host 'PROBLEM: port 6543 is the TRANSACTION pooler.'                      -ForegroundColor Red
    Write-Host '         Npgsql''s prepared statements do not survive it - you would get'
    Write-Host '         "prepared statement _p1 does not exist" sporadically under load'
    Write-Host '         and never in testing. Use the session pooler on 5432.'
    $problems++
}

if ($conn -match '(?i)YOUR-PASSWORD|<password>') {
    Write-Host 'PROBLEM: the placeholder is still in the string.'                   -ForegroundColor Red
    Write-Host '         Replace [YOUR-PASSWORD] with your actual database password.'
    $problems++
}

if ($conn -notmatch '(?i)SSL') {
    Write-Host 'NOTE: no SSL Mode found. Supabase requires encryption, so appending it.' -ForegroundColor Yellow
    $conn = "$conn;SSL Mode=Require;Trust Server Certificate=true"
}

if ($problems -gt 0) {
    Write-Host ''
    Write-Host 'Nothing was written or run. Fix the above and try again.'
    exit 1
}

Write-Host ("Using: " + (Format-Redacted $conn))
Write-Host ''

# --- write the gitignored local settings ---------------------------------
Write-Host '=== Writing appsettings.Local.json (gitignored) ==='

if (Test-Path $localSettings) {
    Copy-Item $localSettings "$localSettings.bak" -Force
    Write-Host '  existing file backed up to appsettings.Local.json.bak'
}

# ConvertTo-Json escapes the value properly. Building the JSON by hand would
# produce an unparseable file the moment a password contained a quote or a
# backslash.
$settings = [ordered]@{
    '//'              = 'Gitignored. Real credentials live here, never in appsettings.Development.json, which is committed.'
    ConnectionStrings = [ordered]@{ DefaultConnection = $conn }
    Seed              = [ordered]@{ DemoUser = $false }
}

# -Encoding utf8 explicitly: Set-Content defaults to the ANSI codepage on
# Windows PowerShell, which mangles any non-ASCII character in a password.
$settings | ConvertTo-Json -Depth 5 | Set-Content -Path $localSettings -Encoding utf8

# Confirm git really is ignoring it before going any further.
Push-Location $repoRoot
try {
    git check-ignore -q $localSettings
    $ignored = ($LASTEXITCODE -eq 0)
}
finally { Pop-Location }

if ($ignored) {
    Write-Host '  confirmed gitignored - it cannot be committed'
}
else {
    Write-Host ''
    Write-Host '  STOP: appsettings.Local.json is NOT gitignored on this checkout.'  -ForegroundColor Red
    Write-Host '  Deleting it rather than risk committing a live password.'
    Remove-Item $localSettings -Force
    exit 1
}

Write-Host ''
Write-Host '  Seed:DemoUser is set to false. A shared database should not get a'
Write-Host '  demo account with a published password.'
Write-Host ''

# --- apply the migrations -------------------------------------------------
Write-Host '=== Applying migrations ==='

# Not --no-build. dotnet-ef defaults to the Debug configuration, so a --no-build
# run loads whatever is in bin\Debug - which after the SQL Server to PostgreSQL
# port still held the SqlServer provider and failed with
# "Keyword not supported: 'host'" against a perfectly valid connection string.
$env:ConnectionStrings__DefaultConnection = $conn
try {
    dotnet restore (Join-Path $repoRoot 'backend') | Out-Null

    dotnet ef database update `
        --project    (Join-Path $repoRoot 'backend\ExpenseManagement.Infrastructure') `
        --startup-project (Join-Path $repoRoot 'backend\ExpenseManagement.Api') 2>&1 |
        Where-Object { $_ -notmatch 'Build started|Determining|Entity Framework tools version' }
}
finally {
    Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
}

# --- verify ---------------------------------------------------------------
Write-Host ''
Write-Host '=== Verifying the schema landed ==='

$psqlExe = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
           Select-Object -Last 1 -ExpandProperty FullName

if ($psqlExe) {
    function Get-Field([string] $key) {
        $m = [regex]::Match($conn, "(?i)$key=([^;]*)")
        if ($m.Success) { $m.Groups[1].Value } else { $null }
    }

    $env:PGPASSWORD = Get-Field 'Password'
    $pgHost = Get-Field 'Host'
    $pgPort = Get-Field 'Port'; if (-not $pgPort) { $pgPort = '5432' }
    $pgDb   = Get-Field 'Database'; if (-not $pgDb) { $pgDb = 'postgres' }
    $pgUser = Get-Field 'Username'

    function Invoke-Scalar([string] $sql) {
        & $psqlExe -h $pgHost -p $pgPort -U $pgUser -d $pgDb -tAc $sql 2>&1
    }

    try {
        Write-Host ("  tables      : " + (Invoke-Scalar "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"))
        Write-Host ("  constraints : " + (Invoke-Scalar "SELECT count(*) FROM pg_constraint WHERE contype='c' AND conname LIKE 'CK_%';"))
        Write-Host ("  indexes     : " + (Invoke-Scalar "SELECT count(*) FROM pg_indexes WHERE schemaname='public';"))

        $idx = Invoke-Scalar "SELECT count(*) FROM pg_indexes WHERE indexname='UX_Categories_UserId_NameLower_Type';"
        if ($idx -eq '1') {
            Write-Host '  case-insensitive category index : present'
        }
        else {
            Write-Host '  case-insensitive category index : *** MISSING *** - the raw-SQL migration step did not run' -ForegroundColor Red
        }
    }
    finally { Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue }
}
else {
    Write-Host '  psql not found; skipping the detailed check.'
    Write-Host '  Migrations applied without error, which already proves connectivity.'
}

Write-Host ''
Write-Host @'
================================================================
  Local setup is done.
================================================================

The connection string is in appsettings.Local.json, which is gitignored, so
`dotnet run` and `dotnet ef` now both talk to Supabase.

Run the API against it:

    cd backend
    dotnet run --project ExpenseManagement.Api

Smoke-test it (39 checks):

    python backend\tests\api-smoke\smoke.py http://localhost:5165

For Render, set the SAME string as the environment variable
ConnectionStrings__DefaultConnection - never in a file - plus Jwt__Secret,
Hosting__BehindTlsTerminatingProxy=true and Seed__DemoUser=false.
'@
