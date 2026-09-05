using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Enums;
using FluentValidation;

namespace ExpenseManagement.Application.Features.Transactions;

/// <summary>
/// Bounds shared by the write validators. The string lengths mirror the column
/// widths exactly: a longer value would otherwise pass validation and then fail
/// as a truncation error the user cannot interpret.
/// </summary>
internal static class TransactionLimits
{
    public const int DescriptionLength = 500;
    public const int MerchantLength = 200;
    public const int NotesLength = 2000;
    public const int ClientReferenceLength = 100;
    public const int SearchLength = 200;

    /// <summary>Comfortably inside decimal(18,2) while still rejecting a fat-fingered paste.</summary>
    public const decimal MaxAmount = 999_999_999_999.99m;

    /// <summary>
    /// Device clocks can legitimately run ahead of UTC by most of a day - a user
    /// in +14:00 entering this morning's coffee is not time travelling - so the
    /// future is only refused beyond that.
    /// </summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromDays(1);

    public static readonly DateTimeOffset EarliestDate = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// The rules a create and an update share, written once against the common
/// interface so the two can never drift apart.
/// </summary>
public abstract class TransactionWriteValidator<T> : AbstractValidator<T>
    where T : ITransactionWriteRequest
{
    protected TransactionWriteValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Choose whether this is an expense or income.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage("Choose how this was paid for.");

        RuleFor(x => x.CategoryId)
            .NotEmpty()
            .WithMessage("Choose a category.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .WithMessage("Enter an amount greater than zero.")
            .LessThanOrEqualTo(TransactionLimits.MaxAmount)
            .WithMessage("That amount is too large to record.");

        RuleFor(x => x.TransactionDate)
            .GreaterThanOrEqualTo(TransactionLimits.EarliestDate)
            .WithMessage("Choose a date from the year 2000 onwards.")
            .Must(date => date <= clock.UtcNow.Add(TransactionLimits.FutureTolerance))
            .WithMessage("Choose a date that is not in the future.");

        RuleFor(x => x.Description)
            .MaximumLength(TransactionLimits.DescriptionLength)
            .WithMessage("That description is too long.");

        RuleFor(x => x.Merchant)
            .MaximumLength(TransactionLimits.MerchantLength)
            .WithMessage("That merchant name is too long.");

        RuleFor(x => x.Notes)
            .MaximumLength(TransactionLimits.NotesLength)
            .WithMessage("Those notes are too long.");

        // Only checked when supplied: an absent code means "use my account
        // currency", which is resolved by the service, not defaulted here.
        RuleFor(x => x.CurrencyCode)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Enter a three-letter currency code, such as USD.")
            .When(x => !string.IsNullOrWhiteSpace(x.CurrencyCode));
    }
}

public sealed class CreateTransactionRequestValidator : TransactionWriteValidator<CreateTransactionRequest>
{
    public CreateTransactionRequestValidator(IDateTimeProvider clock) : base(clock)
    {
        RuleFor(x => x.ClientReference)
            .MaximumLength(TransactionLimits.ClientReferenceLength)
            .WithMessage("That sync reference is too long.");
    }
}

public sealed class UpdateTransactionRequestValidator : TransactionWriteValidator<UpdateTransactionRequest>
{
    public UpdateTransactionRequestValidator(IDateTimeProvider clock) : base(clock)
    {
    }
}

public sealed class TransactionQueryRequestValidator : AbstractValidator<TransactionQueryRequest>
{
    public TransactionQueryRequestValidator()
    {
        // Page and page size are clamped by the base request's setters rather than
        // rejected, so an oversized page is capped instead of 400-ing a client
        // that merely guessed at a limit.
        RuleFor(x => x.Search)
            .MaximumLength(TransactionLimits.SearchLength)
            .WithMessage("That search is too long.");

        RuleFor(x => x.Type)
            .Must(type => type is null || Enum.IsDefined(type.Value))
            .WithMessage("Filter by expenses or income.");

        RuleFor(x => x.PaymentMethod)
            .Must(method => method is null || Enum.IsDefined(method.Value))
            .WithMessage("Filter by a payment method we support.");

        RuleFor(x => x.MinAmount)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("The lowest amount cannot be negative.")
            .When(x => x.MinAmount.HasValue);

        RuleFor(x => x.MaxAmount)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("The highest amount cannot be negative.")
            .When(x => x.MaxAmount.HasValue);

        // An inverted range is worth an explicit message: silently returning
        // nothing reads to the user as "I have no transactions".
        RuleFor(x => x.MaxAmount)
            .Must((query, maxAmount) => maxAmount >= query.MinAmount)
            .WithMessage("The highest amount must not be below the lowest amount.")
            .When(x => x.MinAmount.HasValue && x.MaxAmount.HasValue);

        // Strictly greater, because the range is half-open: From equal to To is an
        // empty window, not a single day.
        RuleFor(x => x.To)
            .Must((query, to) => to > query.From)
            .WithMessage("The end date must be after the start date.")
            .When(x => x.From.HasValue && x.To.HasValue);

        RuleFor(x => x.SortBy)
            .Must(sortBy => IsOneOf(
                sortBy,
                TransactionQueryRequest.SortByDate,
                TransactionQueryRequest.SortByAmount))
            .WithMessage("Sort by date or by amount.")
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy));

        RuleFor(x => x.SortDirection)
            .Must(direction => IsOneOf(
                direction,
                TransactionQueryRequest.SortAscending,
                TransactionQueryRequest.SortDescending))
            .WithMessage("Sort in ascending or descending order.")
            .When(x => !string.IsNullOrWhiteSpace(x.SortDirection));
    }

    private static bool IsOneOf(string? value, params string[] allowed) =>
        Array.Exists(allowed, candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
}
