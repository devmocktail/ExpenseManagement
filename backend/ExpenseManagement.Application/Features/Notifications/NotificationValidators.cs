using FluentValidation;

namespace ExpenseManagement.Application.Features.Notifications;

public sealed class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        DeviceTokenRules.Token(RuleFor(request => request.Token));

        // The token is the routing address for every alert this account sends to
        // this handset, so a platform the sender cannot build a payload for has to
        // be rejected here rather than discovered at delivery time.
        RuleFor(request => request.Platform)
            .IsInEnum()
            .WithMessage("That device type is not one we support.");

        // Both are labels the user may see in a "your devices" list. They are
        // clipped rather than rejected in the service, but a value this far out is
        // a client bug worth reporting instead of silently trimming.
        RuleFor(request => request.DeviceName)
            .MaximumLength(100)
            .WithMessage("That device name is too long.");

        RuleFor(request => request.AppVersion)
            .MaximumLength(20)
            .WithMessage("That app version is too long.");
    }
}

public sealed class UnregisterDeviceRequestValidator : AbstractValidator<UnregisterDeviceRequest>
{
    public UnregisterDeviceRequestValidator()
    {
        DeviceTokenRules.Token(RuleFor(request => request.Token));
    }
}

/// <summary>
/// The one definition of a well-formed push token, shared by registration and
/// deregistration so the two cannot drift into accepting different values for
/// the same string.
/// </summary>
file static class DeviceTokenRules
{
    public static void Token<T>(IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .WithMessage("This device could not be registered for notifications.")
            // Matches the nvarchar(200) column, which is itself sized to stay
            // inside the key limit of the unique index the token is looked up by.
            .MaximumLength(200)
            .WithMessage("This device could not be registered for notifications.");
}
