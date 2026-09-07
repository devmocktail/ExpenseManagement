/*
    DESTRUCTIVE. Drops and recreates the development database.

    Nothing recovers the data afterwards. Only ever point this at a local
    PostgreSQL.

    It cannot run inside a connection to the database it drops, so run it
    against `postgres`:

        psql -h localhost -p 5432 -U postgres -d postgres -f reset-dev.sql

    Then rebuild the schema:

        cd backend
        dotnet ef database update --project ExpenseManagement.Infrastructure \
                                  --startup-project ExpenseManagement.Api

    or just start the API in Development, which migrates on startup.
*/

\set ON_ERROR_STOP on

\echo 'Terminating existing connections to ExpenseManagement...'
-- A single open session — an idle psql, a running API — is enough to make DROP
-- DATABASE fail, so they are closed first.
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'ExpenseManagement' AND pid <> pg_backend_pid();

\echo 'Dropping ExpenseManagement...'
DROP DATABASE IF EXISTS "ExpenseManagement";

\echo 'Recreating ExpenseManagement...'
CREATE DATABASE "ExpenseManagement";

\echo 'Done. Run dotnet ef database update, or start the API in Development.'
