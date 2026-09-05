using FluentValidation;

namespace ExpenseManagement.Application.Features.Recurring;

/// <summary>
/// The rules create and update share, written once against
/// <see cref="IRecurringWriteRequest"/> so the two endpoints cannot accept
/// different things and a message is only ever worded in one place.
///
/// Everything here is shape validation. Whether the category exists, belongs to
/// the caller and matches the schedule's direction is a data question, so it
/// lives in the service where it can be asked of the database.
/// </summary>
public abstract class RecurringWriteRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IRecurringWriteRequest
{
    /// <summary>
    /// Weekly at interval 99 is nearly two years, which is as far as a repeat is
    /// still meaningfully a repeat. The real reason for a ceiling is arithmetic:
    /// an unbounded interval lets a request push occurrences past the end of the
    /// calendar the date types can represent.
    /// </summary>
    private const int MaxInterval = 99;

    protected RecurringWriteRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give this schedule a name.")
            .MaximumLength(100).WithMessage("A schedule name can be at most 100 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Choose whether this is an expense or income.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Enter an amount greater than zero.")
            // Matches the decimal(18,2) column: a third decimal place would be
            // rounded away on save, and the user would then be charged, every
            // period, an amount they never entered.
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("An amount can have at most two decimal places.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Choose a category.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum().WithMessage("Choose a valid payment method.");

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Choose how often this repeats.");

        RuleFor(x => x.Interval)
            .InclusiveBetween(1, MaxInterval)
            .When(x => x.Interval.HasValue)
            .WithMessage($"Repeat every 1 to {MaxInterval} periods.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateTimeOffset)).WithMessage("Choose a start date.");

        // A schedule that ends before it begins can never produce anything, and a
        // schedule that produces nothing is a setting the user thinks is working.
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("The end date must be after the start date.");

        RuleFor(x => x.Merchant)
            .MaximumLength(200).WithMessage("A merchant name can be at most 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("A description can be at most 500 characters.");

        // The upper bound is the database's: a reminder further out than the
        // shortest supported period would fire before the previous occurrence had
        // even been generated.
        RuleFor(x => x.ReminderDaysBefore)
            .InclusiveBetween(0, 30)
            .When(x => x.ReminderDaysBefore.HasValue)
            .WithMessage("A reminder can be set from 0 to 30 days before the due date.");
    }
}

public sealed class CreateRecurringRequestValidator : RecurringWriteRequestValidator<CreateRecurringRequest>
{
}

public sealed class UpdateRecurringRequestValidator : RecurringWriteRequestValidator<UpdateRecurringRequest>
{
}
