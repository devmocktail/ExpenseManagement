using System.Text.RegularExpressions;
using FluentValidation;

namespace ExpenseManagement.Application.Features.Categories;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        CategoryRules.Name(RuleFor(request => request.Name));

        // Type is only settable here. Once a category exists its side of the
        // ledger is fixed, so this is the single point where a bad value could
        // ever reach a stored row.
        RuleFor(request => request.Type)
            .IsInEnum()
            .WithMessage("Choose whether this is an expense or an income category.");

        CategoryRules.Icon(RuleFor(request => request.Icon));
        CategoryRules.Color(RuleFor(request => request.Color));
        CategoryRules.SortOrder(RuleFor(request => request.SortOrder));
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        CategoryRules.Name(RuleFor(request => request.Name));
        CategoryRules.Icon(RuleFor(request => request.Icon));
        CategoryRules.Color(RuleFor(request => request.Color));
        CategoryRules.SortOrder(RuleFor(request => request.SortOrder));
    }
}

public sealed class DeleteCategoryRequestValidator : AbstractValidator<DeleteCategoryRequest>
{
    public DeleteCategoryRequestValidator()
    {
        // Absent is legitimate - it means "this category is unused, just remove
        // it". An all-zero id is not absent, it is a client bug, and letting it
        // through would spend a round-trip only to answer 404.
        RuleFor(request => request.ReassignToCategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Choose a category to move the existing entries to.");
    }
}

/// <summary>
/// The field rules shared by create and edit. File-scoped so the two validators
/// cannot drift apart while nothing else in the assembly has to know these
/// exist.
/// </summary>
file static class CategoryRules
{
    /// <summary>Matches the <c>nchar(7)</c> column and its CK_Categories_Color_Hex check constraint exactly.</summary>
    private const string HexColorPattern = "^#[0-9A-Fa-f]{6}$";

    /// <summary>Lowercase slug the client's icon map is keyed by, e.g. "shopping-bag".</summary>
    private const string IconSlugPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    public static void Name<T>(IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .WithMessage("Give this category a name.")
            .MaximumLength(100)
            .WithMessage("A category name can be at most 100 characters.");

    public static void Icon<T>(IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .WithMessage("Pick an icon for this category.")
            .MaximumLength(50)
            .WithMessage("That icon name is too long.")
            .Matches(IconSlugPattern, RegexOptions.CultureInvariant)
            .WithMessage("That icon is not one the app recognises.");

    public static void Color<T>(IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .WithMessage("Pick a colour for this category.")
            .Matches(HexColorPattern, RegexOptions.CultureInvariant)
            .WithMessage("Use a six-digit colour code, for example #4F46E5.");

    /// <summary>
    /// Null is valid and means "put it at the end of my list" - the comparison
    /// validators pass on a null value, so no When() guard is needed.
    /// </summary>
    public static void SortOrder<T>(IRuleBuilder<T, int?> rule) =>
        rule.InclusiveBetween(0, 100_000)
            .WithMessage("The display position must be between 0 and 100,000.");
}
