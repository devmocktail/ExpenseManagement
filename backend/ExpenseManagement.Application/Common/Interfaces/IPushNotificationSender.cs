namespace ExpenseManagement.Application.Common.Interfaces;

public sealed record PushMessage(
    string To,
    string Title,
    string Body,
    IReadOnlyDictionary<string, object>? Data = null);

/// <summary>Per-token delivery outcome, so dead tokens can be retired.</summary>
public sealed record PushDeliveryResult(
    string Token,
    bool Succeeded,
    string? Error,
    bool TokenIsInvalid);

public interface IPushNotificationSender
{
    Task<IReadOnlyList<PushDeliveryResult>> SendAsync(
        IReadOnlyList<PushMessage> messages,
        CancellationToken cancellationToken = default);
}
