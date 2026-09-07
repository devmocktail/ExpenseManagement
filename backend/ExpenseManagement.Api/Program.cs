using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Api.Filters;
using ExpenseManagement.Api.Middleware;
using ExpenseManagement.Api.Swagger;
using ExpenseManagement.Application;
using ExpenseManagement.Infrastructure;
using ExpenseManagement.Infrastructure.Persistence;
using ExpenseManagement.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

// A bootstrap logger so a failure during host construction — a bad connection
// string, a missing signing key — is reported instead of vanishing.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ---------------------------------------------------------------------
    // Services
    // ---------------------------------------------------------------------

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
    builder.Services.AddJwtAuthentication();

    builder.Services.AddAuthorizationBuilder()
        // Every endpoint requires authentication unless it opts out with
        // [AllowAnonymous]. Making this the default means a new controller added
        // later is secure by omission rather than insecure by omission.
        .SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build())
        .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

    builder.Services
        .AddControllers(options =>
        {
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = false;

            // Global, so every endpoint is validated by default rather than
            // only the ones someone remembered to decorate.
            options.Filters.Add<ValidationFilter>();
        })
        .ConfigureApiBehaviorOptions(options =>
        {
            // MVC's default 400 is a ProblemDetails document, which does not
            // match this API's envelope. Replacing it keeps the client's single
            // error path valid for model-binding failures too.
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .SelectMany(kvp => kvp.Value!.Errors.Select(e =>
                        new ApiFieldError(
                            ToCamelCase(kvp.Key),
                            string.IsNullOrWhiteSpace(e.ErrorMessage)
                                ? "This value is not valid."
                                : e.ErrorMessage)))
                    .ToList();

                return new BadRequestObjectResult(
                    ApiResponse.Fail(
                        "Some of the details are not valid.",
                        "validation_failed",
                        errors,
                        context.HttpContext.TraceIdentifier));
            };
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

            // Nulls are WRITTEN, not omitted. The wire contract declares fields
            // like `merchant: string | null` and `budget: BudgetSummary | null`
            // — a required key whose value may be null, not an optional key.
            // Omitting them produces a payload that does not satisfy the
            // declared types: a generated client, a response schema, or any
            // `Object.hasOwn` check sees the key missing rather than null.
            //
            // The envelope's own optional fields (Data, ErrorCode, Errors,
            // TraceId) carry their own [JsonIgnore] attributes, so they are
            // still omitted without a global default that catches everything.
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;

            // Enums cross the wire as their names. An integer would couple the
            // client to declaration order, so reordering a C# enum would
            // silently change what every stored value means.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddSwaggerDocumentation();

    builder.Services.AddCors(options =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        options.AddPolicy("Default", policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                // A native app sends no Origin header, so it needs no CORS grant
                // at all. An empty allow-list is therefore the correct, and the
                // safest, production default — never AllowAnyOrigin, which would
                // let any website call this API with a user's credentials.
                policy.WithOrigins().AllowAnyHeader().AllowAnyMethod();
            }
            else
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        });
    });

    builder.Services.AddRateLimiter(options =>
    {
        var authPermit = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10);
        var globalPermit = builder.Configuration.GetValue("RateLimiting:GlobalPermitPerMinute", 300);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Tight bucket on the endpoints worth brute-forcing. Partitioned by IP
        // because these are the calls made before anyone is authenticated.
        options.AddPolicy("auth", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        // Everything else is partitioned per user where possible, so one noisy
        // device cannot exhaust the budget for everyone behind the same NAT.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.IsAuthenticated == true
                    ? context.User.FindFirst("sub")?.Value ?? "authenticated"
                    : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPermit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                """
                {"success":false,"message":"Too many attempts. Please wait a moment and try again.","errorCode":"rate_limited","errors":[]}
                """,
                token);
        };
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database");

    // Receipt uploads are the only large bodies this API accepts. The ceiling
    // matches the validator and the database CHECK constraint, with headroom for
    // multipart overhead.
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 12 * 1024 * 1024;
    });

    var app = builder.Build();

    // ---------------------------------------------------------------------
    // Pipeline. Order matters more here than anywhere else in the codebase.
    // ---------------------------------------------------------------------

    // First, so it can catch anything thrown further down.
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

            // The user id is safe to log and makes an incident traceable. The
            // email, the token and the body deliberately are not.
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
            }
        };
    });

    // Must run before anything that reads the scheme or the client IP.
    //
    // Behind a reverse proxy (Render, Railway, an ingress controller, nginx)
    // the app is reached over plain HTTP and the real scheme and caller only
    // survive in X-Forwarded-* headers. Without this:
    //   * Request.IsHttps is always false, so UseHttpsRedirection below would
    //     redirect forever — the proxy re-forwards each redirect as HTTP;
    //   * RemoteIpAddress is the proxy's, so the per-IP rate limiter puts every
    //     user on the planet into one bucket, and the audit trail records the
    //     proxy for every event.
    var forwardedHeaderOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };

    // The known-network lists default to loopback only, which REJECTS headers
    // from a PaaS proxy sitting on an arbitrary internal address — leaving the
    // scheme and client IP exactly as wrong as if this middleware were absent.
    //
    // These have to be .Clear()ed. An object-initializer `KnownNetworks = { }`
    // looks like it empties the list but is collection-initializer syntax: it
    // calls Add() for each of its zero elements and leaves the defaults intact.
    //
    // Trusting any upstream is correct when the platform's proxy is the only
    // route to this container, and NOT correct if it is ever exposed directly —
    // a client could then forge X-Forwarded-For and defeat the per-IP limiter.
    forwardedHeaderOptions.KnownIPNetworks.Clear();
    forwardedHeaderOptions.KnownProxies.Clear();

    app.UseForwardedHeaders(forwardedHeaderOptions);

    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.UseSwaggerDocumentation(app.Services);
    }

    // A PaaS terminates TLS at its edge and forwards HTTP over its private
    // network. Redirecting there is not just redundant, it is a loop. The flag
    // defaults to on so a self-hosted deployment keeps the redirect; set
    // Hosting:BehindTlsTerminatingProxy=true on Render and friends.
    var behindTlsProxy = builder.Configuration.GetValue("Hosting:BehindTlsTerminatingProxy", false);

    if (!app.Environment.IsDevelopment() && !behindTlsProxy)
    {
        // HSTS tells browsers never to try this host over plain HTTP again. It
        // is off in development because a self-signed localhost certificate
        // would then be pinned in the developer's browser.
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseCors("Default");

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    // ---------------------------------------------------------------------
    // Startup work
    // ---------------------------------------------------------------------

    // Startup lifecycle messages carry an explicit SourceContext so they match
    // the "ExpenseManagement" minimum-level override. Serilog's static Log has
    // no SourceContext of its own, so in Production - where the default level is
    // Warning - plain Log.Information calls are silently dropped, and the
    // operator watching a deploy sees nothing between "container started" and
    // whatever fails next.
    var startupLog = Log.ForContext("SourceContext", "ExpenseManagement.Startup");

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Applying migrations from inside the app is a convenience with a real
        // cost: two instances starting together can race on the same schema
        // change, and a failed migration takes the deploy down rather than
        // being a discrete step you can inspect and roll back.
        //
        // It is therefore automatic in Development and opt-in everywhere else.
        // The opt-in exists because a PaaS like Render has no natural place to
        // run a migration step — there is no pre-deploy hook on the free tier,
        // and without this the container starts against an empty database and
        // every request fails. Turn it on with
        // Database__MigrateOnStartup=true, and turn it off again once you have
        // somewhere better to run migrations from.
        var migrateOnStartup = app.Environment.IsDevelopment()
            || builder.Configuration.GetValue("Database:MigrateOnStartup", false);

        // Everything that touches the database at startup is wrapped, because
        // the raw failure is close to unreadable: EF surfaces a connectivity
        // problem as thirty frames of SqlClient internals whose top line says
        // nothing about configuration. What an operator needs is the one
        // sentence naming what to fix.
        try
        {
            if (migrateOnStartup)
            {
                startupLog.Information("Applying database migrations on startup...");
                await db.Database.MigrateAsync();
                startupLog.Information("Migrations are up to date.");
            }

            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Fatal(
                """
                The API could not reach its database, so it is stopping rather than
                serving requests it cannot answer.

                  {Message}

                Check, in this order:
                  1. ConnectionStrings__DefaultConnection is set for this environment.
                  2. The server, database name and credentials in it are correct.
                  3. The database's firewall allows this host's outbound address.
                  4. Encrypt=True is present (Azure SQL requires it).
                  5. On Azure SQL serverless, whether the database is paused - the
                     first connection after an auto-pause can take up to a minute,
                     so raise Connection Timeout in the connection string.

                Full exception follows.
                """,
                ex.Message);

            // Rethrown so the outer handler logs the stack and the process exits
            // non-zero. A container that starts "successfully" without a database
            // just moves the failure to every request.
            throw;
        }
    }

    startupLog.Information(
        "ExpenseManagement API started in {Environment}, listening on {Urls}",
        app.Environment.EnvironmentName,
        string.Join(", ", app.Urls));

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The API terminated unexpectedly during startup.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static string ToCamelCase(string value) =>
    string.IsNullOrEmpty(value) || char.IsLower(value[0])
        ? value
        : char.ToLowerInvariant(value[0]) + value[1..];

/// <summary>
/// Exposed so the integration test project can reference the host through
/// WebApplicationFactory. Top-level statements otherwise generate an internal
/// Program class that a test assembly cannot see.
/// </summary>
public partial class Program;
