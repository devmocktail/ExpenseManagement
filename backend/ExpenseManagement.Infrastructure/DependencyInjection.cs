using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Infrastructure.Identity;
using ExpenseManagement.Infrastructure.Persistence;
using ExpenseManagement.Infrastructure.Persistence.Seeding;
using ExpenseManagement.Infrastructure.Services;
using ExpenseManagement.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddPersistence(configuration, environment);
        services.AddIdentityServices(configuration);
        services.AddStorage(configuration);
        services.AddSupportServices();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // IsNullOrWhiteSpace, not a null check.
        //
        // A `?? throw` looks like fail-fast and is not: a config file carrying
        // `"DefaultConnection": ""` as a placeholder returns an empty string,
        // which is not null, so the guard passes. The provider accepts it
        // happily and the failure surfaces much later, on the first query, as
        // a connection error over a wall of driver stack frames that says
        // nothing about configuration.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                """
                ConnectionStrings:DefaultConnection is not configured.

                This application requires PostgreSQL. Set the connection string
                for the environment it is running in:

                  * Locally      appsettings.Development.json
                  * Container    -e ConnectionStrings__DefaultConnection="..."
                  * Render/PaaS  add ConnectionStrings__DefaultConnection in the
                                 dashboard's environment settings

                The double underscore is the nesting separator: it binds to the
                ConnectionStrings:DefaultConnection key.

                For Supabase, copy the URI from Project Settings > Database and
                convert it to Npgsql's key/value form. Use the SESSION pooler on
                port 5432, not the transaction pooler on 6543 - the latter cannot
                hold the prepared statements Npgsql relies on. See
                docs/deployment.md.
                """);
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                // EnableRetryOnFailure is deliberately NOT set.
                //
                // A retrying execution strategy refuses to run alongside a
                // user-initiated transaction: EF cannot replay a block it did
                // not open, so BeginTransactionAsync throws outright rather
                // than silently retrying half of it. Five operations here need
                // real transactions — registration (account + settings +
                // categories), category reassignment, account closure, refresh
                // rotation, and recurring generation — and every one of them
                // would 500 on its first call.
                //
                // The documented workaround is to wrap each of those in
                // Database.CreateExecutionStrategy().ExecuteAsync(...), which
                // makes the whole block retriable. That is a real change, not a
                // wrapper: a replayed block re-runs against entities the first
                // attempt already mutated, so each one has to be made
                // idempotent before it can be retried safely.
                //
                // Until that work is done, resilience lives where it costs
                // nothing: the mobile client retries 5xx and network failures
                // (see mobile/src/api/query-client.ts). Re-enable this — and do
                // the ExecuteAsync restructure with it — if a hosted Postgres
                // proves flaky enough to warrant it.
                npgsql.CommandTimeout(30);
            });

            if (environment.IsDevelopment())
            {
                // Parameter values in logs are a privacy problem in production
                // (they include amounts, merchants and email addresses) but are
                // the fastest way to debug a query locally.
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IRecurringScheduleReader, RecurringScheduleReader>();
        services.AddScoped<IDeviceTokenReader, DeviceTokenReader>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    private static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup, not at the first login. A missing signing key is
            // the kind of misconfiguration that must never reach production
            // quietly.
            .ValidateOnStart();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                options.User.RequireUniqueEmail = true;

                // Five attempts then a five-minute cooldown. Enough to stop
                // online password guessing without locking out someone who
                // fat-fingers their own password a few times.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;

                // Email confirmation is not required to sign in: this is a
                // personal finance tracker, and blocking access to your own data
                // behind an email round trip is friction with no security gain
                // here. The flag stays available if that changes.
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<TokenService>();
        services.AddScoped<ITokenService>(sp => sp.GetRequiredService<TokenService>());
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddHttpContextAccessor();

        return services;
    }

    private static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IFileValidator, FileValidator>();

        return services;
    }

    private static IServiceCollection AddSupportServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEmailSender, DevelopmentEmailSender>();

        services.AddHttpClient<IPushNotificationSender, ExpoPushNotificationSender>(client =>
        {
            client.BaseAddress = new Uri("https://exp.host/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    /// <summary>
    /// Configures JWT bearer authentication from the same validated
    /// <see cref="JwtOptions"/> the token service signs with, so the issuer and
    /// the validator can never drift into a state where the API mints tokens it
    /// will not accept.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        // Configured through IConfigureOptions rather than the AddJwtBearer
        // callback because the parameters depend on JwtOptions, which is only
        // resolvable from DI. Doing it here also means the options object is
        // built once at startup — assigning TokenValidationParameters per
        // request would be a data race on a singleton.
        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        return services;
    }
}

/// <summary>Builds the bearer scheme's validation parameters from <see cref="JwtOptions"/>.</summary>
internal sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name is not null && name != JwtBearerDefaults.AuthenticationScheme) return;
        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        var jwt = jwtOptions.Value;

        // The token is not needed in HttpContext after validation; the claims
        // are. Not storing it keeps the raw credential out of anything that
        // dumps request state, such as a crash report.
        options.SaveToken = false;

        // Keep the JWT's own claim names ("sub", "email") instead of Microsoft's
        // legacy SOAP-era URIs. CurrentUser reads both spellings, but leaving the
        // mapping on would silently rename claims that policies match against.
        options.MapInboundClaims = false;

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwt.Secret));

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,

            // Pinning the algorithm closes the "alg: none" and asymmetric-to-HMAC
            // confusion attacks, where a forged header persuades the validator to
            // verify with a key the attacker controls.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            // The default five minutes silently extends every token's life by
            // that much, which matters when access tokens are only 15 minutes long.
            ClockSkew = TimeSpan.FromSeconds(30),

            NameClaimType = "name",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Lets the client distinguish "expired, go refresh" from "bad
                // token, sign in again" without parsing the token itself.
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("x-token-expired", "true");
                }

                return Task.CompletedTask;
            },

            OnChallenge = async context =>
            {
                // Without this, an unauthenticated request returns an empty body
                // with a WWW-Authenticate header, which the mobile client's
                // envelope-unwrapping cannot parse and reports as "malformed
                // response" instead of "please sign in".
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(
                    """
                    {"success":false,"message":"You need to sign in to do that.","errorCode":"unauthorized","errors":[]}
                    """);
            },

            OnForbidden = async context =>
            {
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(
                    """
                    {"success":false,"message":"You do not have permission to do that.","errorCode":"forbidden","errors":[]}
                    """);
            },
        };
    }
}
