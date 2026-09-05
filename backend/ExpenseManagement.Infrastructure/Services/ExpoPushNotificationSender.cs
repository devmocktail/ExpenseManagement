using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseManagement.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExpenseManagement.Infrastructure.Services;

/// <summary>
/// Delivers notifications through Expo's push service.
///
/// The <see cref="HttpClient"/> arrives from <c>IHttpClientFactory</c>'s typed
/// client registration, which is the whole point of taking it as a constructor
/// parameter rather than newing one up: the factory pools and rotates the
/// underlying handler, so a long-lived singleton sender neither exhausts sockets
/// nor pins a stale DNS answer for exp.host.
///
/// Nothing in here throws for a delivery problem. Push is best-effort — a dead
/// device must not fail the budget alert for the other four — so every failure
/// comes back as a <see cref="PushDeliveryResult"/> the caller can act on.
/// </summary>
public sealed class ExpoPushNotificationSender(
    HttpClient httpClient,
    ILogger<ExpoPushNotificationSender> logger) : IPushNotificationSender
{
    /// <summary>
    /// Absolute on purpose: the sender then behaves identically whether or not
    /// the registration configured a BaseAddress, and a misconfigured base can
    /// never silently redirect push traffic somewhere else.
    /// </summary>
    private const string PushEndpoint = "https://exp.host/--/api/v2/push/send";

    /// <summary>Expo's documented ceiling for one request.</summary>
    private const int MaxMessagesPerRequest = 100;

    /// <summary>Expo's marker for an app that was uninstalled or whose token rotated.</summary>
    private const string DeviceNotRegistered = "DeviceNotRegistered";

    private const int MaxLoggedBodyLength = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<PushDeliveryResult>> SendAsync(
        IReadOnlyList<PushMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0) return [];

        var results = new List<PushDeliveryResult>(messages.Count);

        // Batches go out one after another rather than in parallel: Expo rate
        // limits per project, and a fan-out that fires ten concurrent requests
        // earns a 429 for the whole run instead of delivering slightly slower.
        foreach (var batch in messages.Chunk(MaxMessagesPerRequest))
        {
            results.AddRange(await SendBatchAsync(batch, cancellationToken));
        }

        return results;
    }

    private async Task<IReadOnlyList<PushDeliveryResult>> SendBatchAsync(
        PushMessage[] batch,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = Array.ConvertAll(batch, ToPayload);

            using var response = await httpClient.PostAsJsonAsync(
                PushEndpoint, payload, SerializerOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadTruncatedBodyAsync(response, cancellationToken);

                logger.LogWarning(
                    "Expo rejected a batch of {Count} push messages with status {StatusCode}: {Body}",
                    batch.Length,
                    (int)response.StatusCode,
                    body);

                // A transport-level rejection says nothing about any individual
                // token, so no token is marked invalid — retiring one on a 429
                // or a 503 would silently unsubscribe a perfectly good device.
                return Failed(batch, $"Expo returned HTTP {(int)response.StatusCode}.");
            }

            var envelope = await response.Content.ReadFromJsonAsync<ExpoTicketEnvelope>(
                SerializerOptions, cancellationToken);

            var tickets = envelope?.Data;

            // Tickets are positional — the only thing tying a ticket to a token is
            // its index — so a short or missing array leaves no safe way to say
            // which device failed. The batch is reported as undelivered rather
            // than guessed at.
            if (tickets is null || tickets.Count != batch.Length)
            {
                logger.LogWarning(
                    "Expo returned {TicketCount} tickets for {MessageCount} messages{RequestErrors}.",
                    tickets?.Count ?? 0,
                    batch.Length,
                    DescribeRequestErrors(envelope));

                return Failed(batch, "Expo returned an unusable response for this batch.");
            }

            var results = new PushDeliveryResult[batch.Length];

            for (var i = 0; i < batch.Length; i++)
            {
                results[i] = Interpret(batch[i].To, tickets[i]);
            }

            return results;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller asked to stop; that is a shutdown, not a delivery
            // failure, and pretending otherwise would retire tokens on deploy.
            throw;
        }
        catch (Exception ex)
        {
            // Covers the network being down, the 30-second client timeout (which
            // surfaces as a cancellation with no cancellation requested) and a
            // malformed body.
            logger.LogError(ex, "Could not reach Expo for a batch of {Count} push messages.", batch.Length);

            return Failed(batch, "Expo could not be reached.");
        }
    }

    /// <summary>
    /// Maps one Expo ticket onto the caller's contract. "ok" means Expo accepted
    /// the message for delivery, not that a phone showed it — the final verdict
    /// lives behind the receipts endpoint — so a successful result here is a
    /// handoff, not a guarantee.
    /// </summary>
    private static PushDeliveryResult Interpret(string token, ExpoTicket ticket)
    {
        if (string.Equals(ticket.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return new PushDeliveryResult(token, Succeeded: true, Error: null, TokenIsInvalid: false);
        }

        var error = ticket.Details?.Error;

        // DeviceNotRegistered is the one error that is permanent and specific to
        // this token: the app is gone or the token rotated. Every other code
        // (MessageTooBig, MessageRateExceeded, InvalidCredentials) is about the
        // payload or the project, so retiring the token would punish the wrong
        // thing.
        var tokenIsInvalid = string.Equals(error, DeviceNotRegistered, StringComparison.OrdinalIgnoreCase);

        return new PushDeliveryResult(
            token,
            Succeeded: false,
            Error: ticket.Message ?? error ?? "Expo rejected the message.",
            TokenIsInvalid: tokenIsInvalid);
    }

    private static PushDeliveryResult[] Failed(PushMessage[] batch, string error) =>
        Array.ConvertAll(
            batch,
            message => new PushDeliveryResult(message.To, Succeeded: false, Error: error, TokenIsInvalid: false));

    private static ExpoPushPayload ToPayload(PushMessage message) => new(
        To: message.To,
        Title: message.Title,
        Body: message.Body,
        Data: message.Data,
        Sound: "default",
        // Budget and bill reminders are time-sensitive by nature; normal priority
        // lets Android hold them until the device next leaves doze, which can be
        // hours after the bill was actually due.
        Priority: "high");

    private static async Task<string> ReadTruncatedBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return body.Length <= MaxLoggedBodyLength ? body : body[..MaxLoggedBodyLength];
        }
        catch (Exception)
        {
            // The status code is the useful part; a body that will not read is not
            // worth turning a logged warning into a thrown exception.
            return string.Empty;
        }
    }

    private static string DescribeRequestErrors(ExpoTicketEnvelope? envelope)
    {
        if (envelope?.Errors is not { Count: > 0 } errors) return string.Empty;

        return " Errors: " + string.Join("; ", errors.Select(e => $"{e.Code}: {e.Message}"));
    }

    private sealed record ExpoPushPayload(
        string To,
        string Title,
        string Body,
        IReadOnlyDictionary<string, object>? Data,
        string Sound,
        string Priority);

    private sealed record ExpoTicketEnvelope(
        IReadOnlyList<ExpoTicket>? Data,
        IReadOnlyList<ExpoRequestError>? Errors);

    private sealed record ExpoTicket(
        string? Status,
        string? Id,
        string? Message,
        ExpoTicketDetails? Details);

    private sealed record ExpoTicketDetails(string? Error);

    private sealed record ExpoRequestError(string? Code, string? Message);
}
