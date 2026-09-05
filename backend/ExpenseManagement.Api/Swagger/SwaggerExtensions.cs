using Asp.Versioning.ApiExplorer;
// Microsoft.OpenApi 2.x (which Swashbuckle 10 depends on) moved the document
// model out of the `.Models` namespace into the root one, and replaced inline
// `OpenApiReference` objects with dedicated `*Reference` types.
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.Extensions.Options;

namespace ExpenseManagement.Api.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            const string schemeId = "Bearer";

            options.AddSecurityDefinition(schemeId, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Paste the access token from POST /api/v1/auth/login. " +
                    "Swagger adds the 'Bearer ' prefix for you.",
            });

            // In OpenAPI 2.x a requirement is keyed by a *reference* to the
            // definition rather than by a second copy of the scheme object, so
            // the two can no longer drift apart.
            // Swashbuckle 10 takes a factory rather than an instance, so the
            // requirement is resolved against the document being generated —
            // which is what lets one definition serve every API version.
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(schemeId)] = [],
            });

            // Include XML doc comments so the summaries written on controllers
            // and DTOs become the endpoint descriptions, rather than duplicating
            // them in attributes.
            var xmlPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            options.SupportNonNullableReferenceTypes();
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));
        });

        services.ConfigureOptions<ConfigureSwaggerGenOptions>();

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(
        this WebApplication app,
        IServiceProvider services)
    {
        var provider = services.GetRequiredService<IApiVersionDescriptionProvider>();

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in provider.ApiVersionDescriptions.Reverse())
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"Expense Management API {description.GroupName.ToUpperInvariant()}");
            }

            options.DocumentTitle = "Expense Management API";
            options.DisplayRequestDuration();
            options.EnablePersistAuthorization();
        });

        return app;
    }
}

/// <summary>
/// Registers one Swagger document per discovered API version. Doing it through
/// IConfigureOptions rather than inline means a future /api/v2 appears
/// automatically instead of needing this file edited.
/// </summary>
internal sealed class ConfigureSwaggerGenOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Expense Management API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "**This version is deprecated.** Migrate to the newest version."
                    : "Personal expense tracking: transactions, budgets, analytics, receipts and recurring rules. "
                      + "Every endpoint except the auth routes requires a bearer token, and every response uses "
                      + "the same `{ success, data, message }` envelope.",
            });
        }
    }
}
