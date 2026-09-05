using ExpenseManagement.Domain.Enums;
using FluentValidation;

namespace ExpenseManagement.Application.Features.Budgets;

/// <summary>
/// The rules create and update share. Written once against
/// <see cref="IBudgetWriteRequest"/> so the two endpoints cannot accept
/// different things and so a message is only ever worded in one place.
/// </summary>
public abstract class BudgetWriteRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IBudgetWriteRequest
{
    protected BudgetWriteRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Give this budget a name.")
            .MaximumLength(100).WithMessage("A budget name can be at most 100 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Enter a budget amount greater than zero.")
            // Matches the decimal(18,2) column: a third decimal place would be rounded
            // away on save and the user would be shown a cap they never set.
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("A budget amount can have at most two decimal places.");

        RuleFor(x => x.Period)
            .IsInEnum().WithMessage("Choose a valid budget period.");

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.CategoryId.HasValue)
            .WithMessage("Choose a category, or leave it empty to budget across all spending.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateTimeOffset)).WithMessage("Choose a start date.");

        // Only a custom window carries its own end date. For every other cadence the
        // server derives one from the period, so an end date sent alongside it is
        // ignored rather than rejected — the client may keep it in its form state.
        When(x => x.Period == BudgetPeriod.Custom, () =>
        {
            RuleFor(x => x.EndDate)
                .NotNull().WithMessage("A custom budget needs an end date.")
                .GreaterThan(x => x.StartDate).WithMessage("The end date must be after the start date.");
        });

        RuleFor(x => x.WarningThreshold)
            .InclusiveBetween(1, 100)
            .When(x => x.WarningThreshold.HasValue)
            .WithMessage("The warning threshold must be between 1 and 100 percent.");

        RuleFor(x => x.CriticalThreshold)
            .InclusiveBetween(1, 100)
            .When(x => x.CriticalThreshold.HasValue)
            .WithMessage("The critical threshold must be between 1 and 100 percent.");

        // Without this the escalation ladder inverts and the louder alert arrives
        // before the gentle one.
        RuleFor(x => x.WarningThreshold)
            .Must((request, warning) =>
                warning is null || request.CriticalThreshold is null || warning < request.CriticalThreshold)
            .WithMessage("The warning threshold must be lower than the critical threshold.");
    }
}

public sealed class CreateBudgetRequestValidator : BudgetWriteRequestValidator<CreateBudgetRequest>
{
}

public sealed class UpdateBudgetRequestValidator : BudgetWriteRequestValidator<UpdateBudgetRequest>
{
}
