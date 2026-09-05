using ExpenseManagement.Domain.Common;
using ExpenseManagement.Domain.Enums;

namespace ExpenseManagement.Domain.Entities;

/// <summary>An Expo push token for one installation of the app.</summary>
public class DeviceToken : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Expo push token, e.g. <c>ExponentPushToken[...]</c>. Unique.</summary>
    public string Token { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }
    public string? DeviceName { get; set; }
    public string? AppVersion { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Set when Expo reports the token as unregistered, so it stops being retried.</summary>
    public DateTimeOffset? InvalidatedAt { get; set; }
}
