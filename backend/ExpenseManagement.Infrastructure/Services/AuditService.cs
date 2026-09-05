using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseManagement.Infrastructure.Services;

/// <summary>
/// Writes the append-only audit trail.
///
/// Two properties matter more than anything else here: an audit write must not
/// change the outcome of the request it describes, and it must never become a
/// second copy of the secrets that request carried. Almost everything below
/// exists to hold one of those two lines.
/// </summary>
public sealed class AuditService(
    DbContextOptions<AppDbContext> contextOptions,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditService> logger) : IAuditService
{
    /// <summary>
    /// Any property whose name contains one of these fragments is replaced
    /// wholesale. Substring matching on purpose: it catches <c>resetToken</c>,
    /// <c>PasswordHash</c> and <c>refresh_token</c> without anyone having to
    /// remember to register each new spelling.
    /// </summary>
    private static readonly string[] SensitiveKeyFragments =
        ["password", "token", "secret", "authorization", "hash"];

    private const string Redacted = "[redacted]";

    // Mirrors the column widths in AuditLogConfiguration. Exceeding a mapped max
    // length makes SQL Server reject the INSERT, which would lose an entire
    // audit row over a long user-agent string; a clipped value still identifies
    // the client.
    private const int ActionMaxLength = 100;
    private const int EntityNameMaxLength = 100;
    private const int IpAddressMaxLength = 45;
    private const int UserAgentMaxLength = 400;
    private const int MetadataMaxLength = 4000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public async Task LogAsync(
        string action,
        Guid? userId = null,
        string? entityName = null,
        Guid? entityId = null,
        bool succeeded = true,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var http = httpContextAccessor.HttpContext;

        // An explicitly supplied id wins over the ambient principal: a failed
        // login knows which account was targeted even though nobody is
        // authenticated on that request, and a token refresh audits the owner of
        // the presented token rather than an identity that does not exist yet.
        var subjectId = userId ?? currentUser.UserId;

        try
        {
            var entry = new AuditLog
            {
                UserId = subjectId,
                Action = Truncate(action, ActionMaxLength) ?? "unknown",
                EntityName = Truncate(entityName, EntityNameMaxLength),
                EntityId = entityId,
                Succeeded = succeeded,
                IpAddress = Truncate(http?.Connection.RemoteIpAddress?.ToString(), IpAddressMaxLength),
                UserAgent = Truncate(http?.Request.Headers.UserAgent.ToString(), UserAgentMaxLength),
                MetadataJson = SerialiseRedacted(action, metadata),
            };

            // Written through a context of its own rather than the request's, for
            // two reasons. A shared context would flush whatever the caller has
            // tracked but not yet saved, turning "log this attempt" into an early
            // commit of a half-built change — and a failed audit save would leave
            // the entry stuck in the caller's change tracker, so the next
            // SaveChanges anywhere in the request would fail too. The row also has
            // to outlive a rollback of the operation it describes, because "they
            // tried and it failed" is precisely the event worth keeping.
            // CreatedAt is still stamped by SaveChanges, and AuditLog carries no
            // query filters, so building the context without a principal costs
            // nothing here.
            await using var db = new AppDbContext(contextOptions);

            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A client hanging up mid-request is routine, not a fault worth an
            // error-level line in every log aggregator.
            logger.LogDebug("Audit entry {Action} was abandoned because the request was cancelled.", action);
        }
        catch (Exception ex)
        {
            // The audit trail is evidence, not a precondition. A database that
            // will not take the row must not turn a password change that already
            // succeeded into a 500 for the user who just made it.
            logger.LogError(
                ex,
                "Failed to write audit entry {Action} for user {UserId}.",
                action,
                subjectId);
        }
    }

    /// <summary>
    /// Serialises caller-supplied context to JSON with every sensitive value
    /// stripped. Redaction runs over the serialised tree rather than the original
    /// object so anonymous types, dictionaries and nested DTOs are all walked by
    /// the same code and none of them can opt out.
    /// </summary>
    private string? SerialiseRedacted(string action, object? metadata)
    {
        if (metadata is null) return null;

        try
        {
            var node = JsonSerializer.SerializeToNode(metadata, SerializerOptions);
            if (node is null) return null;

            Redact(node);

            var json = node.ToJsonString(SerializerOptions);

            // Clipping the string would leave invalid JSON in a column every
            // future investigation has to parse, so an oversized payload becomes
            // a marker rather than a fragment.
            return json.Length <= MetadataMaxLength
                ? json
                : $"{{\"truncated\":true,\"originalLength\":{json.Length}}}";
        }
        catch (Exception ex)
        {
            // A reference cycle or an unserialisable type is a bug at the call
            // site, not a reason to lose the fact that the event happened.
            logger.LogWarning(ex, "Could not serialise audit metadata for {Action}.", action);
            return null;
        }
    }

    private static void Redact(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                // Keys are materialised first: assigning into the object while
                // enumerating it invalidates the enumerator.
                foreach (var key in obj.Select(property => property.Key).ToArray())
                {
                    if (IsSensitive(key))
                    {
                        // Replaces the whole subtree, not just scalar leaves — a
                        // property named "token" holding an object is exactly the
                        // shape a credential tends to arrive in.
                        obj[key] = Redacted;
                        continue;
                    }

                    if (obj[key] is { } child) Redact(child);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null) Redact(item);
                }

                break;
        }
    }

    private static bool IsSensitive(string propertyName) =>
        SensitiveKeyFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
