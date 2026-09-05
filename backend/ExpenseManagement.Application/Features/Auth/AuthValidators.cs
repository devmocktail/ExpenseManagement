using FluentValidation;

namespace ExpenseManagement.Application.Features.Auth;

/// <summary>
/// Rules that more than one auth request needs, kept in one place so register,
/// reset and change can never disagree about what a valid password is — a drift
/// there shows up as a password that is accepted at registration and rejected
/// the first time the user tries to change it.
/// </summary>
internal static class AuthRules
{
    /// <summary>
    /// Mirrors <c>passwordSchema</c> in <c>mobile/src/validation/schemas.ts</c>.
    /// The client shows these instantly as a checklist; this side is the one that
    /// actually enforces them, because anything checked only on a device can be
    /// skipped by talking to the API directly.
    ///
    /// Deliberately no <c>NotEmpty</c>: the length rule already rejects an empty
    /// value, and adding one would put two messages under the field for the same
    /// mistake. Every remaining rule runs, so the response lists exactly the
    /// requirements still unmet rather than one at a time.
    /// </summary>
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .MinimumLength(8).WithMessage("Use at least 8 characters.")
            .MaximumLength(128).WithMessage("That password is too long.")
            .Matches("[A-Z]").WithMessage("Add an uppercase letter.")
            .Matches("[a-z]").WithMessage("Add a lowercase letter.")
            .Matches("[0-9]").WithMessage("Add a number.")
            .Matches("[^A-Za-z0-9]").WithMessage("Add a special character.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Enter your email address.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256).WithMessage("That email address is too long.");
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        // Length is measured after trimming: "  " clears a plain MinimumLength(2)
        // but the service stores the trimmed value, which the database's
        // CK_Users_FullName_NotEmpty then rejects as a 409 nobody can act on.
        RuleFor(x => x.FullName)
            .Must(name => name is not null && name.Trim().Length >= 2)
                .WithMessage("Enter your name.")
            .MaximumLength(200).WithMessage("That name is too long.");

        RuleFor(x => x.Email).ValidEmail();

        RuleFor(x => x.Password).ValidPassword();

        // Attached to the confirmation rather than the password so the message
        // lands under the field the user has to retype.
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("Please accept the terms to continue.");

        // Both hints are optional and the service falls back to the account
        // defaults, so these only reject values that are definitely malformed —
        // a stricter list of currencies here would go stale the first time the
        // product launches somewhere new.
        RuleFor(x => x.CurrencyCode!)
            .Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter currency code, such as INR.")
            .When(x => !string.IsNullOrWhiteSpace(x.CurrencyCode));

        RuleFor(x => x.TimeZoneId!)
            .MaximumLength(100).WithMessage("That time zone name is not one we recognise.")
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZoneId));
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();

        // The password policy is deliberately not applied here. Sign-in must keep
        // working for accounts created before the current rules, and rejecting a
        // short password at the door would also tell an attacker their guess was
        // never even checked.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Enter your password.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Enter the code from your email.");

        RuleFor(x => x.NewPassword).ValidPassword();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Enter your current password.");

        RuleFor(x => x.NewPassword)
            .ValidPassword()
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("Choose a password you have not used here before.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
