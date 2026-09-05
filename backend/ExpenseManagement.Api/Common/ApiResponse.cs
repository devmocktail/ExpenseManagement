using System.Text.Json.Serialization;

namespace ExpenseManagement.Api.Common;

/// <summary>
/// The response envelope every endpoint returns, success or failure.
///
/// A uniform shape means the mobile client has exactly one unwrapping path and
/// one error path, instead of branching on whether a particular endpoint felt
/// like returning a bare object, a ProblemDetails, or a raw string.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    /// <summary>Null on failure. Never serialised on failure responses.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    /// <summary>Human-readable and safe to show to a user. Never a stack trace.</summary>
    public string? Message { get; init; }

    /// <summary>Stable machine code the client branches on. Only present on failure.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    /// <summary>Field-level validation failures. Empty (not null) on non-validation errors.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<ApiFieldError>? Errors { get; init; }

    /// <summary>
    /// Correlates this response with the server log line. Safe to display —
    /// it identifies a request, not a user, and turns "it broke" into something
    /// support can actually look up.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(
        string message,
        string errorCode,
        IReadOnlyList<ApiFieldError>? errors = null,
        string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors ?? [],
            TraceId = traceId,
        };
}

/// <summary>Envelope for endpoints with no payload (204-style operations that still return 200).</summary>
public sealed class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<ApiFieldError>? Errors { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    public static ApiResponse Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(
        string message,
        string errorCode,
        IReadOnlyList<ApiFieldError>? errors = null,
        string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors ?? [],
            TraceId = traceId,
        };
}

/// <summary>
/// One field-level failure. <see cref="Field"/> is camelCased to match the JSON
/// the client sent, so React Hook Form can map it straight onto the input that
/// needs the message.
/// </summary>
public sealed record ApiFieldError(string Field, string Message);
