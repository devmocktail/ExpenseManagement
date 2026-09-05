using ExpenseManagement.Domain.Enums;
using FluentValidation;

namespace ExpenseManagement.Application.Features.Profile;

/// <summary>
/// Helpers for the settings PATCH, where every property is nullable and null
/// means "not sent". A plain <c>InclusiveBetween</c> would have to be paired
/// with a <c>When</c> at every call site, and the one that got forgotten would
/// reject an untouched setting the user never mentioned.
/// </summary>
internal static class SettingsRules
{
    public static IRuleBuilderOptions<T, int?> InRangeWhenPresent<T>(
        this IRuleBuilder<T, int?> rule,
        int min,
        int max,
        string message) =>
        rule.Must(value => value is null || (value >= min && value <= max)).WithMessage(message);
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        // Measured after trimming, because the service stores the trimmed value:
        // "  " clears a plain MinimumLength(2) and then hits
        // CK_Users_FullName_NotEmpty as a 409 the user cannot act on.
        RuleFor(x => x.FullName)
            .Must(name => name is not null && name.Trim().Length >= 2)
                .WithMessage("Enter your name.")
            .MaximumLength(200).WithMessage("That name is too long.");
    }
}

public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        // Deliberately shape-only, and case-insensitive to match registration's
        // currency hint. A hard-coded list of live ISO 4217 codes would go stale
        // the first time the product launches somewhere new, and the cost of a
        // typo'd code is a wrong label rather than wrong arithmetic — amounts
        // carry their own currency on the transaction row.
        RuleFor(x => x.CurrencyCode!)
            .Matches("^[A-Za-z]{3}$").WithMessage("Use a three-letter currency code, such as INR.")
            .When(x => x.CurrencyCode is not null);

        RuleFor(x => x.Locale!)
            .Matches("^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8}){0,4}$")
                .WithMessage("Use a language tag, such as en-IN.")
            .MaximumLength(20).WithMessage("That language tag is too long.")
            .When(x => x.Locale is not null);

        RuleFor(x => x.TimeZoneId!)
            .Must(BeAResolvableZone)
                .WithMessage("That time zone is not one we recognise.")
            .MaximumLength(100).WithMessage("That time zone name is too long.")
            .When(x => x.TimeZoneId is not null);

        RuleFor(x => x.Theme)
            .Must(theme => theme is null || Enum.IsDefined(theme.Value))
                .WithMessage("Choose system, light or dark.");

        // These three mirror the CHECK constraints on UserSettings. Restating a
        // database rule in a validator is duplication with a purpose: the
        // constraint protects the data, this protects the user from meeting it
        // as an unexplained 500.
        RuleFor(x => x.BudgetWarningThreshold)
            .InRangeWhenPresent(1, 100, "Set the warning alert between 1% and 100%.");

        RuleFor(x => x.BudgetCriticalThreshold)
            .InRangeWhenPresent(1, 100, "Set the critical alert between 1% and 100%.");

        // Only checkable here when the request carries both. A PATCH that moves
        // one against the other's stored value is caught by the service, which is
        // the only place that can see the merged pair.
        RuleFor(x => x.BudgetCriticalThreshold)
            .Must((request, critical) =>
                critical is null
                || request.BudgetWarningThreshold is null
                || critical >= request.BudgetWarningThreshold)
            .WithMessage("Your critical alert must be at or above your warning alert.");

        // 28 rather than 31: a period anchored on the 29th has no start date in
        // February, and the period generator would have to silently pick a
        // different day for some months.
        RuleFor(x => x.MonthStartDay)
            .InRangeWhenPresent(1, 28, "Choose a day between 1 and 28.");
    }

    /// <summary>
    /// Tested with <see cref="TimeZoneInfo.TryFindSystemTimeZoneById"/> rather
    /// than <c>PeriodCalculator.ResolveTimeZone</c>: that one answers UTC for
    /// anything it cannot resolve, by design, so it can never report a failure.
    /// Storing a zone this server cannot resolve would leave every period
    /// boundary quietly computed in UTC — an Asia/Kolkata user's evening
    /// spending would land in the previous day, month after month, with nothing
    /// anywhere saying why.
    /// </summary>
    private static bool BeAResolvableZone(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out _);
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        // No password policy here, only presence. This is a re-authentication of
        // an existing password, and an account created under older rules must
        // still be closable by the person who owns it.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Enter your password to confirm.");
    }
}

public sealed class ExportTransactionsRequestValidator : AbstractValidator<ExportTransactionsRequest>
{
    public ExportTransactionsRequestValidator()
    {
        RuleFor(x => x.Format)
            .Must(format => Enum.IsDefined(format)).WithMessage("Choose either csv or json.");

        // The bounds are half-open, so equal dates select nothing at all — worth
        // rejecting rather than handing back an empty file that looks like an
        // account with no transactions in it.
        RuleFor(x => x.To)
            .Must((request, to) => request.From is null || to is null || to > request.From)
                .WithMessage("The end date must be after the start date.");
    }
}
