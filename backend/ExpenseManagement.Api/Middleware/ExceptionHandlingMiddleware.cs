using System.Text.Json;
using ExpenseManagement.Api.Common;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManagement.Api.Middleware;

/// <summary>
/// The single place an unhandled exception becomes an HTTP response.
///
/// Two guarantees matter here:
///
///  * A caller never sees a stack trace, a SQL error, or a type name. Those
///    disclose schema and framework versions and are exactly what an attacker
///    reads first. The client gets a written-for-humans message and a trace id.
///  * The server always sees the whole thing. Everything unexpected is logged at
///    Error with the full exception before the sanitised response goes out.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        // Once the response has started there is no way to replace it with an
        // error body; the best we can do is log and let the connection drop,
        // which the client sees as a network failure.
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "Exception after the response had started; cannot write an error body.");
            throw exception;
        }

        var traceId = context.TraceIdentifier;
        var (status, errorCode, message, errors) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}. TraceId {TraceId}",
                context.Request.Method,
                context.Request.Path,
                traceId);
        }
        else
        {
            // Expected failures are noise at Error level but genuinely useful at
            // Information when diagnosing a client integration.
            logger.LogInformation(
                "Request failed with {Status} {ErrorCode} on {Method} {Path}. TraceId {TraceId}",
                status,
                errorCode,
                context.Request.Method,
                context.Request.Path,
                traceId);
        }

        // In Development the real message is far more useful than a generic one,
        // and there is no attacker to protect against on a developer's machine.
        var body = ApiResponse.Fail(
            environment.IsDevelopment() && status >= 500
                ? $"{message} ({exception.GetType().Name}: {exception.Message})"
                : message,
            errorCode,
            errors,
            traceId);

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(body, JsonOptions.Default),
            context.RequestAborted);
    }

    private static (int Status, string ErrorCode, string Message, IReadOnlyList<ApiFieldError>? Errors)
        Map(Exception exception) => exception switch
        {
            NotFoundException ex =>
                (StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message, null),

            ConflictException ex =>
                (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message, null),

            ForbiddenException ex =>
                (StatusCodes.Status403Forbidden, ex.ErrorCode, ex.Message, null),

            BusinessRuleException ex =>
                (StatusCodes.Status422UnprocessableEntity, ex.ErrorCode, ex.Message, null),

            FluentValidation.ValidationException ex => (
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "Some of the details are not valid.",
                ex.Errors
                    .Select(e => new ApiFieldError(ToCamelCase(e.PropertyName), e.ErrorMessage))
                    .ToList()),

            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "unauthorized", "You need to sign in to do that.", null),

            // A cancelled request is the client hanging up, not a server fault.
            // 499 is nginx's convention; nothing is written to a closed socket
            // anyway, so this only affects what gets logged.
            OperationCanceledException =>
                (499, "request_cancelled", "The request was cancelled.", null),

            // Surfaces when a unique index is violated by a race that the service
            // could not pre-check. The message stays generic because the index
            // name would disclose schema.
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "concurrency_conflict",
                    "Someone else changed this while you were editing it. Reload and try again.", null),

            DbUpdateException =>
                (StatusCodes.Status409Conflict, "data_conflict",
                    "That conflicts with something that already exists.", null),

            _ => (StatusCodes.Status500InternalServerError, "server_error",
                    "Something went wrong on our side. Please try again.", null),
        };

    /// <summary>
    /// FluentValidation reports PascalCase property paths; the client sent
    /// camelCase JSON and matches errors to inputs by name, so the two have to
    /// agree. Handles nested paths like <c>Items[0].CategoryId</c>.
    /// </summary>
    private static string ToCamelCase(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath)) return propertyPath;

        return string.Join('.', propertyPath.Split('.').Select(segment =>
            segment.Length == 0 || char.IsLower(segment[0])
                ? segment
                : char.ToLowerInvariant(segment[0]) + segment[1..]));
    }
}

/// <summary>Shared serializer options so middleware output matches MVC output exactly.</summary>
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,

        // Matches the MVC pipeline exactly — see the note in Program.cs. Nulls
        // are written; the envelope's optional fields opt out individually via
        // their own [JsonIgnore] attributes.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };
}
