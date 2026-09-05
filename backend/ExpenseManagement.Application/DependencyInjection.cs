using System.Reflection;
using ExpenseManagement.Application.Features.Analytics;
using ExpenseManagement.Application.Features.Auth;
using ExpenseManagement.Application.Features.Budgets;
using ExpenseManagement.Application.Features.Categories;
using ExpenseManagement.Application.Features.Dashboard;
using ExpenseManagement.Application.Features.Notifications;
using ExpenseManagement.Application.Features.Profile;
using ExpenseManagement.Application.Features.Receipts;
using ExpenseManagement.Application.Features.Recurring;
using ExpenseManagement.Application.Features.Transactions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped, because every service depends on the request-scoped DbContext
        // and ICurrentUser. Registering any of these as a singleton would capture
        // one user's identity for the lifetime of the process — the single worst
        // bug this codebase could have.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRecurringService, RecurringService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Picks up every AbstractValidator in this assembly, so adding a
        // validator never requires editing this file — the commonest way a new
        // endpoint silently ships unvalidated.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        return services;
    }
}
