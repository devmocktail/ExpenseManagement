using ExpenseManagement.Api.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ExpenseManagement.Api.Filters;

/// <summary>
/// Runs the FluentValidation validator for every bound action argument.
///
/// FluentValidation stopped shipping MVC auto-validation in v11, so registering
/// validators in DI no longer causes anything to call them. Without this filter
/// the validators in the Application layer are dead code: a request with a
/// negative amount sails past them and is caught only by the database CHECK
/// constraint, which surfaces as an opaque 409 instead of "Amount must be
/// greater than zero" attached to the amount field.
///
/// Applied globally rather than per-action, so a new endpoint is validated by
/// default instead of only when someone remembers the attribute.
/// </summary>
public sealed class ValidationFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var errors = new List<ApiFieldError>();

        foreach (var argument in context.ActionArguments)
        {
            if (argument.Value is null) continue;

            var argumentType = argument.Value.GetType();

            // Resolved by closed generic type: there is no non-generic entry
            // point that would let one lookup serve every argument.
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

            if (serviceProvider.GetService(validatorType) is not IValidator validator) continue;

            var validationContext = new ValidationContext<object>(argument.Value);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (result.IsValid) continue;

            errors.AddRange(result.Errors.Select(failure =>
                new ApiFieldError(ToCamelCase(failure.PropertyName), failure.ErrorMessage)));
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(
                ApiResponse.Fail(
                    "Some of the details are not valid.",
                    "validation_failed",
                    errors,
                    context.HttpContext.TraceIdentifier));

            return;
        }

        await next();
    }

    /// <summary>
    /// FluentValidation reports PascalCase paths; the client sent camelCase JSON
    /// and maps errors back onto inputs by name, so the two have to agree.
    /// Handles nested paths such as <c>Items[0].CategoryId</c>.
    /// </summary>
    private static string ToCamelCase(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath)) return propertyPath;

        return string.Join('.', propertyPath.Split('.').Select(segment =>
            segment.Length == 0 || char.IsLower(segment[0])
                ? segment
                : char.ToLowerInvariant(segment[0]) + segment[1..]));
    }
}
