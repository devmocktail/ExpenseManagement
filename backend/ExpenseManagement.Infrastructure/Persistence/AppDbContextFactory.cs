using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ExpenseManagement.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting the API.
///
/// Design-time tooling otherwise has to boot the whole host to find a
/// connection string, which fails whenever the app needs configuration the
/// developer's shell does not have. This reads the API's appsettings directly
/// and falls back to LocalDB, so <c>dotnet ef migrations add</c> works from a
/// clean clone with no environment set up.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDbFallback =
        "Server=(localdb)\\MSSQLLocalDB;Database=ExpenseManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveApiProjectPath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? LocalDbFallback;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContextFactory).Assembly.FullName))
            .Options;

        // The single-argument constructor leaves CurrentUserId null. That is
        // correct for tooling: migrations only need the model, and a null user
        // means no query this context runs could return another user's data.
        return new AppDbContext(options);
    }

    /// <summary>
    /// Walks up from the Infrastructure project to find the API project, which
    /// owns the configuration files. Falls back to the current directory so the
    /// factory still works when invoked from an unexpected location.
    /// </summary>
    private static string ResolveApiProjectPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var i = 0; i < 5 && directory is not null; i++)
        {
            var candidate = Path.Combine(directory.FullName, "ExpenseManagement.Api");
            if (Directory.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
