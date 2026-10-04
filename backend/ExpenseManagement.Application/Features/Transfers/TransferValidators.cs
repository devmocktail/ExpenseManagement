using FluentValidation;

namespace ExpenseManagement.Application.Features.Transfers;

public sealed class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferRequestValidator()
    {
        TransferRules.FromAccountId(RuleFor(request => request.FromAccountId));
        TransferRules.ToAccountId(RuleFor(request => request.ToAccountId));
        TransferRules.Amount(RuleFor(request => request.Amount));
        TransferRules.TransferDate(RuleFor(request => request.TransferDate));
        TransferRules.Notes(RuleFor(request => request.Notes));

        // Caught here as well as by the check constraint and the service. This
        // is the only one of the three that can name the field, so the client
        // highlights the picker the user has to change.
        RuleFor(request => request)
            .Must(request => request.FromAccountId != request.ToAccountId)
            .WithName(nameof(CreateTransferRequest.ToAccountId))
            .WithMessage("Choose a different account to transfer into.");

        RuleFor(request => request.ClientReference)
            .MaximumLength(100)
            .WithMessage("Client reference can be at most 100 characters.");
    }
}

public sealed class UpdateTransferRequestValidator : AbstractValidator<UpdateTransferRequest>
{
    public UpdateTransferRequestValidator()
    {
        TransferRules.FromAccountId(RuleFor(request => request.FromAccountId));
        TransferRules.ToAccountId(RuleFor(request => request.ToAccountId));
        TransferRules.Amount(RuleFor(request => request.Amount));
        TransferRules.TransferDate(RuleFor(request => request.TransferDate));
        TransferRules.Notes(RuleFor(request => request.Notes));

        RuleFor(request => request)
            .Must(request => request.FromAccountId != request.ToAccountId)
            .WithName(nameof(UpdateTransferRequest.ToAccountId))
            .WithMessage("Choose a different account to transfer into.");
    }
}

internal static class TransferRules
{
    public static void FromAccountId<T>(IRuleBuilderInitial<T, Guid> rule) =>
        rule.NotEmpty().WithMessage("Choose the account the money is coming from.");

    public static void ToAccountId<T>(IRuleBuilderInitial<T, Guid> rule) =>
        rule.NotEmpty().WithMessage("Choose the account the money is going to.");

    /// <summary>
    /// Strictly positive. Direction is carried by the two account ids, so a
    /// negative amount would be a second and contradictory way to express it —
    /// and a zero transfer is a row that claims money moved when none did.
    /// </summary>
    public static void Amount<T>(IRuleBuilderInitial<T, decimal> rule) =>
        rule.GreaterThan(0)
            .WithMessage("Enter how much you are moving.")
            .LessThan(1_000_000_000_000m)
            .WithMessage("That amount is out of range.")
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Amounts can have at most two decimal places.");

    /// <summary>
    /// A small forward tolerance rather than none. Clocks drift, and a phone a
    /// few minutes fast should not have its entry rejected — but a transfer
    /// dated next month would quietly vanish from this month's balances.
    /// </summary>
    public static void TransferDate<T>(IRuleBuilderInitial<T, DateTimeOffset> rule) =>
        rule.NotEmpty()
            .WithMessage("Pick the date the money moved.")
            .Must(date => date <= DateTimeOffset.UtcNow.AddDays(1))
            .WithMessage("A transfer cannot be dated in the future.");

    public static void Notes<T>(IRuleBuilderInitial<T, string?> rule) =>
        rule.MaximumLength(2000)
            .WithMessage("Notes can be at most 2000 characters.");
}
