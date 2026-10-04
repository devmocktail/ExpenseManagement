using System.Text.RegularExpressions;
using FluentValidation;

namespace ExpenseManagement.Application.Features.Accounts;

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        AccountRules.Name(RuleFor(request => request.Name));

        RuleFor(request => request.Type)
            .IsInEnum()
            .WithMessage("Choose what kind of account this is.");

        // Only settable here. An account's currency is fixed once it exists,
        // because changing it would reinterpret every amount already recorded
        // against it, so this is the one point a bad value could reach a row.
        AccountRules.CurrencyCode(RuleFor(request => request.CurrencyCode));

        AccountRules.OpeningBalance(RuleFor(request => request.OpeningBalance));
        AccountRules.Institution(RuleFor(request => request.Institution));
        AccountRules.Last4(RuleFor(request => request.Last4));
        AccountRules.Icon(RuleFor(request => request.Icon));
        AccountRules.Color(RuleFor(request => request.Color));
        AccountRules.SortOrder(RuleFor(request => request.SortOrder));
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        AccountRules.Name(RuleFor(request => request.Name));

        RuleFor(request => request.Type)
            .IsInEnum()
            .WithMessage("Choose what kind of account this is.");

        AccountRules.OpeningBalance(RuleFor(request => request.OpeningBalance));
        AccountRules.Institution(RuleFor(request => request.Institution));
        AccountRules.Last4(RuleFor(request => request.Last4));
        AccountRules.Icon(RuleFor(request => request.Icon));
        AccountRules.Color(RuleFor(request => request.Color));
        AccountRules.SortOrder(RuleFor(request => request.SortOrder));
    }
}

/// <summary>
/// Shared rules, so the create and update validators cannot drift apart and
/// accept on one path what the other rejects.
/// </summary>
internal static class AccountRules
{
    private static readonly Regex HexColor =
        new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FourDigits =
        new("^[0-9]{4}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IsoCurrency =
        new("^[A-Za-z]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Name<T>(IRuleBuilderInitial<T, string> rule) =>
        rule.NotEmpty()
            .WithMessage("Give the account a name.")
            .MaximumLength(100)
            .WithMessage("Account names can be at most 100 characters.");

    public static void CurrencyCode<T>(IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(code => code is null || IsoCurrency.IsMatch(code))
            .WithMessage("Currency must be a three-letter ISO code such as INR or USD.");

    /// <summary>
    /// Deliberately permits negatives: a credit card's balance normally starts
    /// below zero, and refusing that would force the user to misrepresent it.
    /// The bound exists only to catch a client sending something nonsensical.
    /// </summary>
    public static void OpeningBalance<T>(IRuleBuilderInitial<T, decimal?> rule) =>
        rule.Must(value => value is null || Math.Abs(value.Value) < 1_000_000_000_000m)
            .WithMessage("That opening balance is out of range.");

    public static void Institution<T>(IRuleBuilderInitial<T, string?> rule) =>
        rule.MaximumLength(100)
            .WithMessage("Institution names can be at most 100 characters.");

    /// <summary>
    /// Four digits and no more. The field is a label for telling two cards
    /// apart; a full card number must never reach storage, and bounding the
    /// length is what makes that structural rather than a matter of trust.
    /// </summary>
    public static void Last4<T>(IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(value => string.IsNullOrEmpty(value) || FourDigits.IsMatch(value))
            .WithMessage("Enter only the last four digits.");

    public static void Icon<T>(IRuleBuilderInitial<T, string?> rule) =>
        rule.MaximumLength(50)
            .WithMessage("Icon names can be at most 50 characters.");

    public static void Color<T>(IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(value => value is null || HexColor.IsMatch(value))
            .WithMessage("Colour must look like #4F46E5.");

    public static void SortOrder<T>(IRuleBuilderInitial<T, int?> rule) =>
        rule.Must(value => value is null or (>= 0 and <= 9999))
            .WithMessage("Sort order must be between 0 and 9999.");
}
