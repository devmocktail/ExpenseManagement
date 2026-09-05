/*
    DESTRUCTIVE. Drops the development database.

    Nothing recovers the data afterwards. Only ever point this at LocalDB.

    The guard below refuses to run anywhere that is not LocalDB, because the one
    way this script causes real harm is being pasted into a window that is still
    connected to a shared server.

    After running, either start the API (it migrates automatically in
    Development) or apply migrations by hand:

        cd backend
        dotnet ef database update --project ExpenseManagement.Infrastructure ^
                                  --startup-project ExpenseManagement.Api
*/

SET NOCOUNT ON;

IF SERVERPROPERTY('ServerName') NOT LIKE '%LOCALDB%'
BEGIN
    RAISERROR(
        'Refusing to run: this connection is not LocalDB. reset-dev.sql only ever targets a throwaway development database.',
        16, 1);
    SET NOEXEC ON;
END
GO

USE master;
GO

IF DB_ID('ExpenseManagement') IS NOT NULL
BEGIN
    PRINT 'Closing existing connections to ExpenseManagement...';
    ALTER DATABASE ExpenseManagement SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    PRINT 'Dropping ExpenseManagement...';
    DROP DATABASE ExpenseManagement;

    PRINT 'Dropped. Start the API (Development) or run dotnet ef database update to rebuild it.';
END
ELSE
BEGIN
    PRINT 'ExpenseManagement does not exist; nothing to drop.';
END
GO

SET NOEXEC OFF;
GO
